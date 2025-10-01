using CopyTrading.Mappers;
using CopyTrading.Models.Enums;
using CopyTrading.Models.Trade;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.SQLite;
using CopyTrading.Repository.SQLite.Dto;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using CopyTrading.Values;
using TradeRepositoreySQL = CopyTrading.Repository.SQLite.TradeRepository;
using TradeRepositoryInflux = CopyTrading.Repository.Influx.TradeRepository;

namespace CopyTrading.Services;

public class TradeService(
    OrdersTradesSubscriber _orderProvider,
    OrderBookSubscriber _orderBookProvider,
    IWalletInfoProvider _walletInfoProvider,
    TradeRepositoryInflux _tradeRepositoryInflux,
    TradeRepositoreySQL _tradeRepositorySQL,
    WalletInfoRepository _walletInfoRepository,
    InformationService _informationService,
    CurrentWalletPositionService _currentWalletPositionService,
    ILogger<TradeService> _logger)
{
    public async Task SubscribeToWalletTrades(Wallet wallet)
    {
        await SubscribeToWallet(wallet);
    }

    public async Task SubscribeToTrackedWalletsTrades()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            await SubscribeToWallet(wallet);
        }
    }

    public async Task CollectHystoricalTrades(Wallet wallet)
    {
        var trades = await _walletInfoProvider.GetHistoricalTrades(wallet);

        _tradeRepositoryInflux.WriteTrades(trades);
    }

    private async Task SubscribeToWallet(Wallet wallet)
    {
        var walletInfo = await _walletInfoProvider.GetInfo(wallet, false);
        var walletSnapshot = walletInfo.ToWalletSnapshot();

        _currentWalletPositionService.InitializeWalletSnapshot(walletSnapshot);

        _walletInfoRepository.WriteCurrentPositions(new WalletSnapshotPositionsDto(walletSnapshot));

        await _orderProvider.SubscribeToFilledTrades(wallet, OnNewTrades);
    }

    private void OnNewTrades((Trade[] Trades, bool IsSnapshot) newTrades)
    {
        foreach (var trade in newTrades.Trades)
        {
            if (!trade.IsFuture) continue;

            _tradeRepositorySQL.WriteTrade(trade);

            if (newTrades.IsSnapshot)
            {                
                continue;
            }

            CalculateWriteMinPerpEquityForTrade(trade);
        }
    }

    private async Task CalculateWriteMinPerpEquityForTrade(Trade trade)
    {
        var (spread, deltaTimeS) = await ActualSpreadAndDeltaTime(trade);

        var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet);

        var minPE = _informationService.CalculateMinPerpEquity(walletInfo.AccountVolume, trade.VolumeUsd);
        var subType = await _currentWalletPositionService.AddTrade(trade);

        //_logger.LogWarning($"subType {subType.ToString()}");

        _tradeRepositorySQL.WriteMinPeForTrade(new MinPEForTrade
        {
            TradeId = trade.TradeId,
            AccountVolume = walletInfo.AccountVolume,
            DeltaTimeS = deltaTimeS,
            MinPE = minPE,
            Spread = spread,
            SubType = subType,
        });
    }

    private async Task<(decimal spread, decimal deltaTimeS)> ActualSpreadAndDeltaTime(Trade trade)
    {
        var spreadDelta = 0.01M;

        var orderBook = await _orderBookProvider.GetOrderBook(trade.Symbol, trade.TimeStamp);

        if (orderBook is null) return (0, 0);

        var bestAsk = orderBook.Levels.Asks.First().Price;
        var bestBid = orderBook.Levels.Bids.First().Price;

        Console.WriteLine($"Trade Time : {trade.TimeStamp}\n" +
                          $"OB    Time : {orderBook.Timestamp}");

        var deltaTimeS = (decimal)(orderBook.Timestamp - trade.TimeStamp).TotalSeconds;
        decimal spread;

        if (trade.Direction == Direction.Long)
        {
            spread = (bestAsk - trade.Price) / bestAsk * 100;

            if (trade.Price >= bestAsk || spread < spreadDelta)
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} актуальна для покупки. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
            else
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} не актуальна для покупки. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
        }
        else
        {
            spread = (bestBid - trade.Price) / bestBid * 100;

            if (trade.Price <= bestBid || spread < spreadDelta)
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} актуальна для продажи. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
            else
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} не актуальна для продажи. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
        }

        return (spread, deltaTimeS);
    }
}
