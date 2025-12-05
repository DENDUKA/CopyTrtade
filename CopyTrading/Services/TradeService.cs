using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using ITradeRepositorySQL = CopyTrading.Repository.SQLite.ITradeRepository;
using ITradeRepositoryInflux = CopyTrading.Repository.Influx.Interfaces.ITradeRepository;
using CopyTrading.Providers.Hyperliquid.Interfaces;

namespace CopyTrading.Services;

public class TradeService : ITradeService
{
    private readonly IOrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfoProvider;
    private readonly ITradeRepositoryInflux _tradeRepositoryInflux;
    private readonly ITradeRepositorySQL _tradeRepositorySQL;
    private readonly IFillsOrderService _fillsOrderService;
    private readonly ICurrentWalletPositionService _currentWalletPositionService;
    private readonly IBaselinePositionService _baselinePositionService;
    private readonly BlazorUI.Services.Interfaces.IRealtimeUpdateService _realtimeUpdateService;
    private readonly ILogger<TradeService> _logger;

    public TradeService(
        IOrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfoProvider,
        ITradeRepositoryInflux tradeRepositoryInflux,
        ITradeRepositorySQL tradeRepositorySQL,
        IFillsOrderService fillsOrderService,
        ICurrentWalletPositionService currentWalletPositionService,
        IBaselinePositionService baselinePositionService,
        BlazorUI.Services.Interfaces.IRealtimeUpdateService realtimeUpdateService,
        ILogger<TradeService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfoProvider = walletInfoProvider;
        _tradeRepositoryInflux = tradeRepositoryInflux;
        _tradeRepositorySQL = tradeRepositorySQL;
        _fillsOrderService = fillsOrderService;
        _currentWalletPositionService = currentWalletPositionService;
        _baselinePositionService = baselinePositionService;
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

    private async Task SubscribeToWallet(Wallet[] wallets)
    {
        await _orderProvider.SubscribeToTrades(wallets);
    }

    private async void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        try
        {
            await ProcessNewTrades(newTrades);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"TradeService.OnNewTrades: Критическая ошибка при обработке трейдов. IsSnapshot={newTrades.IsSnapshot}, Count={newTrades.Trades.Length}");
        }
    }

    private async Task ProcessNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        // Фильтруем только фьючерсные трейды
        var futureTrades = newTrades.Trades.Where(t => t.IsFuture).ToArray();

        if (futureTrades.Length == 0)
        {
            _logger.LogDebug($"TradeService.ProcessNewTrades: Нет фьючерсных трейдов для обработки. Total={newTrades.Trades.Length}");
            return;
        }

        // Записываем трейды в БД (fire-and-forget - не ждем ответа от БД)
        SaveTrades(futureTrades, newTrades.IsSnapshot);

        // Создаем новый tuple с отфильтрованными трейдами
        var filteredNewTrades = (futureTrades, newTrades.IsSnapshot);

        // Распространяем события в другие сервисы
        _realtimeUpdateService.OnNewTrades(filteredNewTrades);
        _fillsOrderService.OnNewTrades(filteredNewTrades);
        await _currentWalletPositionService.OnNewTrades(filteredNewTrades);
        await _baselinePositionService.OnNewTrades(filteredNewTrades);

        _logger.LogDebug($"TradeService.ProcessNewTrades: Обработано {futureTrades.Length} фьючерсных трейдов. IsSnapshot={newTrades.IsSnapshot}");
    }

    private void SaveTrades(OriginalTrade[] trades, bool isSnapshot)
    {
        foreach (var trade in trades)
        {
            try
            {
                // Fire-and-forget: не ждем ответа от БД для максимальной производительности
                _ = _tradeRepositorySQL.WriteTrade(trade);

                // Логируем только новые трейды (не snapshot) и только с DEBUG уровнем для уменьшения шума
                if (!isSnapshot)
                {
                    _logger.LogDebug($"TradeService.SaveTrades: TradeId={trade.TradeId} OrderId={trade.OrderId} Symbol={trade.Symbol} Direction={trade.Direction} Price={trade.Price} Quantity={trade.Quantity}");

                    // Логируем задержку с сервером для анализа производительности
                    var delay = (DateTime.Now - trade.TimeStamp).TotalSeconds;
                    if (delay > 1.0) // Логируем только если задержка > 1 секунды
                    {
                        _logger.LogWarning($"TradeService.SaveTrades: TradeId={trade.TradeId} OrderId={trade.OrderId} Большая задержка с сервером: {delay:F2}s");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"TradeService.SaveTrades: TradeId={trade.TradeId} OrderId={trade.OrderId} Ошибка при записи трейда в БД");
                // Продолжаем обработку остальных трейдов даже при ошибке
            }
        }
    }
}
