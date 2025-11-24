using CopyTrading.BlazorUI.Services;
using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using ITradeRepositorySQL = CopyTrading.Repository.SQLite.ITradeRepository;
using ITradeRepositoryInflux = CopyTrading.Repository.Influx.ITradeRepository;

namespace CopyTrading.Services;

public class TradeService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly OrderBookSubscriber _orderBookProvider;
    private readonly IWalletInfoProvider _walletInfoProvider;
    private readonly ITradeRepositoryInflux _tradeRepositoryInflux;
    private readonly ITradeRepositorySQL _tradeRepositorySQL;
    private readonly FillsOrderService _fillsOrderService;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly RealtimeUpdateService _realtimeUpdateService;
    private readonly ILogger<TradeService> _logger;

    public TradeService(
        OrdersTradesSubscriber orderProvider,
        OrderBookSubscriber orderBookProvider,
        IWalletInfoProvider walletInfoProvider,
        ITradeRepositoryInflux tradeRepositoryInflux,
        ITradeRepositorySQL tradeRepositorySQL,
        FillsOrderService fillsOrderService,
        CurrentWalletPositionService currentWalletPositionService,
        RealtimeUpdateService realtimeUpdateService,
        ILogger<TradeService> logger)
    {
        _orderProvider = orderProvider;
        _orderBookProvider = orderBookProvider;
        _walletInfoProvider = walletInfoProvider;
        _tradeRepositoryInflux = tradeRepositoryInflux;
        _tradeRepositorySQL = tradeRepositorySQL;
        _fillsOrderService = fillsOrderService;
        _currentWalletPositionService = currentWalletPositionService;
        _realtimeUpdateService = realtimeUpdateService;
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

            _logger.LogInformation($"TradeService.OnNewTrades: TradeId={trade.TradeId} Новый trade: {trade.ToString()}");

            LogDelayWithServer(trade);
        }


        _realtimeUpdateService.OnNewTrades(newTrades);
        _fillsOrderService.OnNewTrades(newTrades);
        await _currentWalletPositionService.OnNewTrades(newTrades);
    }


    private async Task<(decimal spread, decimal deltaTimeS)> ActualSpreadAndDeltaTime(OriginalTrade trade)
    {
        var spreadDelta = 0.01M;

        var orderBook = await _orderBookProvider.GetOrderBook(trade.Symbol, trade.TimeStamp);

        if (orderBook is null) return (0, 0);

        var bestAsk = orderBook.Levels.Asks.First().Price;
        var bestBid = orderBook.Levels.Bids.First().Price;

        _logger.LogInformation($"TradeService.ActualSpreadAndDeltaTime: TradeId={trade.TradeId} Trade Time: {trade.TimeStamp}, OB Time: {orderBook.Timestamp}");

        var deltaTimeS = (decimal)(orderBook.Timestamp - trade.TimeStamp).TotalSeconds;
        decimal spread;

        if (trade.Direction == Direction.Long)
        {
            spread = (bestAsk - trade.Price) / bestAsk * 100;

            if (trade.Price >= bestAsk || spread < spreadDelta)
            {
                _logger.LogInformation($"TradeService.ActualSpreadAndDeltaTime: TradeId={trade.TradeId} Direction={trade.Direction} Цена сделки {trade.Price} сейчас {bestAsk} актуальна для покупки. Spread={spread:F5}%, DeltaTime={deltaTimeS}s");
            }
            else
            {
                _logger.LogInformation($"TradeService.ActualSpreadAndDeltaTime: TradeId={trade.TradeId} Direction={trade.Direction} Цена сделки {trade.Price} сейчас {bestAsk} не актуальна для покупки. Spread={spread:F5}%, DeltaTime={deltaTimeS}s");
            }
        }
        else
        {
            spread = (bestBid - trade.Price) / bestBid * 100;

            if (trade.Price <= bestBid || spread < spreadDelta)
            {
                _logger.LogInformation($"TradeService.ActualSpreadAndDeltaTime: TradeId={trade.TradeId} Direction={trade.Direction} Цена сделки {trade.Price} сейчас {bestBid} актуальна для продажи. Spread={spread:F5}%, DeltaTime={deltaTimeS}s");
            }
            else
            {
                _logger.LogInformation($"TradeService.ActualSpreadAndDeltaTime: TradeId={trade.TradeId} Direction={trade.Direction} Цена сделки {trade.Price} сейчас {bestBid} не актуальна для продажи. Spread={spread:F5}%, DeltaTime={deltaTimeS}s");
            }
        }

        return (spread, deltaTimeS);
    }

    private void LogDelayWithServer(OriginalTrade trade)
    {
        var now = DateTime.Now;
        _logger.LogInformation($"TradeService.LogDelayWithServer: TradeId={trade.TradeId} OrderId={trade.OrderId} Delay={(now - trade.TimeStamp).TotalSeconds}s");
    }
}
