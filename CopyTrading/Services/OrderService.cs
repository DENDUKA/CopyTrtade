using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using SQLLiteOrderRepository = CopyTrading.Repository.SQLite.OrderRepository;
using TradeRepositorySQL = CopyTrading.Repository.SQLite.TradeRepository;

namespace CopyTrading.Services;

public class OrderService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfo;
    private readonly OrderRepository _orderDBProvider;
    private readonly IExchangeInfoProvider _exchangeInfoProvider;
    private readonly SQLLiteOrderRepository _orderSQLLiteRepository;
    private readonly TradeRepositorySQL _tradeRepositorySQL;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly FillsOrderService _fillsOrderService;
    private readonly CopyOrderService _copyOrderService;
    private readonly BlazorUI.Services.RealtimeUpdateService _realtimeUpdateService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfo,
        OrderRepository orderDBProvider,
        IExchangeInfoProvider exchangeInfoProvider,
        SQLLiteOrderRepository orderSQLLiteProvider,
        TradeRepositorySQL tradeRepositorySQL,
        CurrentWalletPositionService currentWalletPositionService,
        FillsOrderService fillsOrderService,
        CopyOrderService copyOrderService,
        BlazorUI.Services.RealtimeUpdateService realtimeUpdateService,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _orderSQLLiteRepository = orderSQLLiteProvider;
        _currentWalletPositionService = currentWalletPositionService;
        _tradeRepositorySQL = tradeRepositorySQL;
        _fillsOrderService = fillsOrderService;
        _copyOrderService = copyOrderService;
        _realtimeUpdateService = realtimeUpdateService;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
    }

    public async Task SubscribeToWalletOrders(Wallet wallet)
    {
        await _orderProvider.SubscribeToNewOrders([wallet]);
    }

    public async Task SubscribeToTrackedWalletsOrders()
    {
        await _orderProvider.SubscribeToNewOrders(WalletSettings.TrackedWallets);
    }

    public async Task CollectHistoryOrders()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            var orders = await _walletInfo.GetHistoricalOrders(wallet);

            _orderDBProvider.WriteOrder(orders);

            _logger.LogInformation($"Исторические ордера для кошелька {wallet} собраны. {orders.Length}");
        }
    }

    private async void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var order in orders)
        {
            _logger.LogInformation($"Получен новый ордер: {order}");

            // Рассчитываем SubType для каждого нового ордера
            order.SubType = _currentWalletPositionService.GetOrderSubType(order);

            _orderSQLLiteRepository.WriteOrder(order);

            var minPeForOrder = await CalculateMinPerpEquityForOrder(order);
            _tradeRepositorySQL.WriteMinPeForOrder(minPeForOrder);

            LogDelayWithServer(order);
        }

        _realtimeUpdateService.OnNewOrders(orders);
        _fillsOrderService.OnNewOrders(orders);

        // Пересчитываем SubType для всех pending ордеров, затронутых изменениями
        RecalculateSubTypesForOrders(orders);

        await _copyOrderService.OnNewOrders(orders);
    }

    private static decimal CalculateMinPerpEquity(decimal walletVolume, decimal tradeVolume)
    {
        return 100 / (tradeVolume / walletVolume * 100);
    }

    private async Task<MinPEForOrder> CalculateMinPerpEquityForOrder(OriginalOrder order)
    {
        var walletInfo = await _walletInfo.GetInfo(order.Wallet);

        var minPE = CalculateMinPerpEquity(walletInfo.AccountVolume, order.VolumeUsd);

        return new MinPEForOrder
        {
            OrderId = order.OrderId,
            AccountVolume = walletInfo.AccountVolume,
            MinPE = minPE,
            SubType = order.SubType,
        };
    }

    /// <summary>
    /// Пересчитывает SubType для всех pending ордеров, затронутых изменениями
    /// Группирует ордера по (Wallet, Symbol) и вызывает пересчёт для каждой группы
    /// </summary>
    private void RecalculateSubTypesForOrders(OriginalOrder[] orders)
    {
        var orderGroups = orders
            .GroupBy(o => new { o.Wallet, o.Symbol })
            .ToArray();

        foreach (var group in orderGroups)
        {
            _currentWalletPositionService.RecalculateSubTypesForSymbol(group.Key.Wallet, group.Key.Symbol);
        }
    }

    private void LogDelayWithServer(OriginalOrder order)
    {
        var now = DateTime.Now;
        _logger.LogInformation($"LogDelayWithServer Order {order.Status.ToString()} {order.OrderId} {(now - order.Time).TotalSeconds} ");
    }
}