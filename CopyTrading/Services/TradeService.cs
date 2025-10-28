using CopyTrading.DataEvents;
using CopyTrading.Mappers;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.SQLite;
using CopyTrading.Repository.SQLite.Dto;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using TradeRepositoreySQL = CopyTrading.Repository.SQLite.TradeRepository;
using TradeRepositoryInflux = CopyTrading.Repository.Influx.TradeRepository;

namespace CopyTrading.Services;

public class TradeService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly OrderBookSubscriber _orderBookProvider;
    private readonly IWalletInfoProvider _walletInfoProvider;
    private readonly TradeRepositoryInflux _tradeRepositoryInflux;
    private readonly TradeRepositoreySQL _tradeRepositorySQL;
    private readonly WalletInfoRepository _walletInfoRepository;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly FillsOrderService _fillsOrderService;
    private readonly ILogger<TradeService> _logger;

    public TradeService(
        OrdersTradesSubscriber orderProvider,
        OrderBookSubscriber orderBookProvider,
        IWalletInfoProvider walletInfoProvider,
        TradeRepositoryInflux tradeRepositoryInflux,
        TradeRepositoreySQL tradeRepositorySQL,
        WalletInfoRepository walletInfoRepository,
        CurrentWalletPositionService currentWalletPositionService,
        FillsOrderService fillsOrderService,
        ILogger<TradeService> logger)
    {
        _orderProvider = orderProvider;
        _orderBookProvider = orderBookProvider;
        _walletInfoProvider = walletInfoProvider;
        _tradeRepositoryInflux = tradeRepositoryInflux;
        _tradeRepositorySQL = tradeRepositorySQL;
        _walletInfoRepository = walletInfoRepository;
        // ✅ Оставляем зависимость для InitializeWalletSnapshot (используется в SubscribeToWallet)
        _currentWalletPositionService = currentWalletPositionService;
        _fillsOrderService = fillsOrderService;
        _logger = logger;

        DataBusEvents.NewTrades += OnNewTrades;
    }

    public async Task SubscribeToWalletTrades(Wallet wallet)
    {
        await SubscribeToWallet([wallet]);
    }

    public async Task SubscribeToTrackedWalletsTrades()
    {
        await SubscribeToWallet(WalletSettings.TrackedWallets);
    }

    public async Task CollectHystoricalTrades(Wallet wallet)
    {
        var trades = await _walletInfoProvider.GetHistoricalTrades(wallet);

        _tradeRepositoryInflux.WriteTrades(trades);
    }

    private async Task SubscribeToWallet(Wallet[] wallets)
    {
        foreach (var wallet in wallets)
        {
            var walletInfo = await _walletInfoProvider.GetInfo(wallet, false);
            var walletSnapshot = walletInfo.ToWalletSnapshot();

            _currentWalletPositionService.InitializeWalletSnapshot(walletSnapshot);

            _walletInfoRepository.WriteCurrentPositions(new WalletSnapshotPositionsDto(walletSnapshot));
        }

        await _orderProvider.SubscribeToTrades(wallets);
    }

    private async void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        foreach (var trade in newTrades.Trades)
        {
            if (!trade.IsFuture) continue;

            _tradeRepositorySQL.WriteTrade(trade);

            if (newTrades.IsSnapshot)
            {
                continue;
            }

            _logger.LogInformation($"Новый trade : {trade.ToString()}");

            // ✅ CurrentWalletPositionService теперь сам подписан на DataBusEvents.NewTrades
            // await _currentWalletPositionService.AddTrade(trade);
        }

        _fillsOrderService.OnNewTrades(newTrades);
    }


    private async Task<(decimal spread, decimal deltaTimeS)> ActualSpreadAndDeltaTime(OriginalTrade trade)
    {
        var spreadDelta = 0.01M;

        var orderBook = await _orderBookProvider.GetOrderBook(trade.Symbol, trade.TimeStamp);

        if (orderBook is null) return (0, 0);

        var bestAsk = orderBook.Levels.Asks.First().Price;
        var bestBid = orderBook.Levels.Bids.First().Price;

        _logger.LogInformation($"Trade Time : {trade.TimeStamp}\n" +
                          $"OB    Time : {orderBook.Timestamp}");

        var deltaTimeS = (decimal)(orderBook.Timestamp - trade.TimeStamp).TotalSeconds;
        decimal spread;

        if (trade.Direction == Direction.Long)
        {
            spread = (bestAsk - trade.Price) / bestAsk * 100;

            if (trade.Price >= bestAsk || spread < spreadDelta)
            {
                _logger.LogInformation($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} актуальна для покупки. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
            else
            {
                _logger.LogInformation($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} не актуальна для покупки. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
        }
        else
        {
            spread = (bestBid - trade.Price) / bestBid * 100;

            if (trade.Price <= bestBid || spread < spreadDelta)
            {
                _logger.LogInformation($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} актуальна для продажи. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
            else
            {
                _logger.LogInformation($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} не актуальна для продажи. {spread:F5} % d времени Trade и OrderBook {deltaTimeS} Sec");
            }
        }

        return (spread, deltaTimeS);
    }
}
