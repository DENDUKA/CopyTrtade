using CopyTrading.BlazorUI.Services.Interfaces;
using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using IOrderRepositoryInflux = CopyTrading.Repository.InfluxInterfaces.IOrderRepository;
using ISQLLiteOrderRepository = CopyTrading.Repository.SQLInterfaces.Interfaces.IOrderRepository;
using ITradeRepositorySQL = CopyTrading.Repository.SQLInterfaces.Interfaces.ITradeRepository;

namespace CopyTrading.Services;

public class OrderService : IOrderService
{
    private readonly IOrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfo;
    private readonly IOrderRepositoryInflux _orderDBProvider;
    private readonly ISQLLiteOrderRepository _orderSQLLiteRepository;
    private readonly ITradeRepositorySQL _tradeRepositorySQL;
    private readonly ICurrentWalletPositionService _currentWalletPositionService;
    private readonly IFillsOrderService _fillsOrderService;
    private readonly ICopyOrderService _copyOrderService;
    private readonly IRealtimeUpdateService _realtimeUpdateService;
    private readonly IOrdersProvider _ordersProvider;
    private readonly ICopyOrderStorageService _copyOrderStorageService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfo,
        IOrderRepositoryInflux orderDBProvider,
        ISQLLiteOrderRepository orderSQLLiteProvider,
        ITradeRepositorySQL tradeRepositorySQL,
        ICurrentWalletPositionService currentWalletPositionService,
        IFillsOrderService fillsOrderService,
        ICopyOrderService copyOrderService,
        IRealtimeUpdateService realtimeUpdateService,
        IOrdersProvider ordersProvider,
        ICopyOrderStorageService copyOrderStorageService,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _orderSQLLiteRepository = orderSQLLiteProvider;
        _currentWalletPositionService = currentWalletPositionService;
        _tradeRepositorySQL = tradeRepositorySQL;
        _fillsOrderService = fillsOrderService;
        _copyOrderService = copyOrderService;
        _realtimeUpdateService = realtimeUpdateService;
        _ordersProvider = ordersProvider;
        _copyOrderStorageService = copyOrderStorageService;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
        DataBusEvents.CopyOrderCancelRequested += OnCopyOrderCancelRequested;
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

            _logger.LogInformation($"OrderService.Activate: Wallet={wallet} Собрано {orders.Length} исторических ордеров");
        }
    }

    private async void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var order in orders)
        {
            _logger.LogInformation($"OrderService.OnNewOrders: OrderId={order.OrderId} Получен новый ордер {order.Symbol} {order.Direction} Status={order.Status}");

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
        _logger.LogInformation($"OrderService.LogDelayWithServer: OrderId={order.OrderId} Status={order.Status} Delay={(now - order.Time).TotalSeconds}s");
    }

    /// <summary>
    /// Обработчик события отмены копируемого ордера.
    /// Вызывается когда CopyOrderService2 публикует событие CopyOrderCancelRequested
    /// </summary>
    /// <param name="originalOrderId">ID оригинального ордера трейдера</param>
    private void OnCopyOrderCancelRequested(long originalOrderId)
    {
        _logger.LogInformation($"OrderService.OnCopyOrderCancelRequested: OriginalOrderId={originalOrderId} Получено событие отмены копируемого ордера");

        try
        {
            CloseOrderWithStatus(originalOrderId, OrderStatus.Canceled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"OrderService.OnCopyOrderCancelRequested: OriginalOrderId={originalOrderId} Ошибка при отмене копируемого ордера");
        }
    }

    /// <summary>
    /// Отменяет копируемый ордер на бирже
    /// </summary>
    public void CloseOrderWithStatus(long orderId, OrderStatus status)
    {
        var copyOrders = _copyOrderStorageService.GetOrdersByOriginalOrderId(orderId);

        foreach (var copyOrder in copyOrders)
        {
            _logger.LogInformation($"OrderService CancelCopyOrder {copyOrder.ToString()}");
            var oldStatus = copyOrder.OriginalOrder.Status;

            // Обновляем статус
            copyOrder.OriginalOrder.Status = OrderStatus.Canceled;

            _logger.LogInformation(
                $"Копируемый ордер закрыт: ID={copyOrder.OrderId}, {oldStatus} -> {OrderStatus.Canceled.ToString()}, " +
                $"Symbol={copyOrder.OriginalOrder.Symbol}, Direction={copyOrder.OriginalOrder.Direction}, " +
                $"OriginalOrderId={copyOrder.OriginalOrderId}");
        }
    }
}