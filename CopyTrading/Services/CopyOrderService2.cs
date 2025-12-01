using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService2(
    IActiveWindowService _activeWindowService,
    IBaselinePositionService _baselinePositionService,
    ICurrentWalletPositionService _currentWalletPositionService,
    ICopyOrderStorageService _copyOrderStorageService,
    IOrderService _orderService,
    ILogger<CopyOrderService2> _logger) : ICopyOrderService
{
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public async Task OnNewOrders(OriginalOrder[] orders)
    {
        ArgumentNullException.ThrowIfNull(orders, nameof(orders));

        var marketOrders = orders.Where(o => o.Type == OrderType.Market).ToArray();
        var limitOrders = orders.Where(o => o.Type == OrderType.Limit).ToArray();
        var noneOrders = orders.Where(o => o.Type == OrderType.None).ToArray();

        OnMarketOrdersHandler(marketOrders);
        OnLimitOrdersHandler(marketOrders);
        OnNoneOrdersHandler(marketOrders);
    }

    private async Task OnLimitOrdersHandler(OriginalOrder[] orders)
    {
        if (orders.Length == 0) return;

        var orderGroups = orders
            .GroupBy(order => (order.Wallet, order.Symbol))
            .ToArray();

        _logger.LogInformation($"CopyOrderService2.OnNewOrders: Получено {orders.Length} ордеров в {orderGroups.Length} группах (Wallet, Symbol)");

        // Обрабатываем каждую группу
        foreach (var group in orderGroups)
        {
            var (wallet, symbol) = group.Key;
            var groupOrders = group.ToArray();

            ProcessOrdersForWalletAndSymbol(wallet, symbol);
        }
    }



    private async Task OnMarketOrdersHandler(OriginalOrder[] orders)
    {

    }

    /// <summary>
    /// Обрабатывает группу ордеров для одного кошелька и символа
    /// </summary>
    private void ProcessOrdersForWalletAndSymbol(Wallet wallet, string symbol)
    {
        var nearestLongOrders = _activeWindowService.GetNearestOrders(wallet, symbol, Direction.Long, count: 3);
        var nearestShortOrders = _activeWindowService.GetNearestOrders(wallet, symbol, Direction.Short, count: 3);

        var copyedOrders = _copyOrderStorageService.GetOrdersByWalletAndSymbol(wallet, symbol);

        var allNearestOrders = nearestLongOrders.Concat(nearestShortOrders).ToArray();

        // Определяем какие ордера нужно скопировать и какие отменить
        var (ordersToCopy, ordersToCancel) = FindOrdersToProcessing(allNearestOrders, copyedOrders);

        CancelOrders(ordersToCancel);

        _logger.LogInformation(
            $"CopyOrderService2.ProcessOrdersForWalletAndSymbol: Wallet={wallet.Value}, Symbol={symbol}, " +
            $"NearestLong={nearestLongOrders.Length}, NearestShort={nearestShortOrders.Length}, " +
            $"AlreadyCopied={copyedOrders.Length}, ToCopy={ordersToCopy.Length}, ToCancel={ordersToCancel.Length}");
    }

    private void CancelOrders(CopyOrderV2[] ordersToCancel)
    {
        if (ordersToCancel.Length == 0) return;

        foreach (var copyOrder in ordersToCancel)
        {
            _orderService.CloseOrderWithStatus(copyOrder.OriginalOrderId, OrderStatus.Canceled);
        }


    }

    /// <summary>
    /// Находит ордера для копирования и отмены на основе ближайших и уже скопированных
    /// </summary>
    /// <param name="allNearestOrders">Все ближайшие ордера трейдера (Long + Short)</param>
    /// <param name="copyOrdersForPair">Уже скопированные ордера для данной пары (wallet, symbol)</param>
    /// <returns>Кортеж: (ордера для копирования, ордера для отмены)</returns>
    private static (OrderFills[] OrdersToCopy, CopyOrderV2[] OrdersToCancel) FindOrdersToProcessing(
        OrderFills[] allNearestOrders,
        CopyOrderV2[] copyOrdersForPair)
    {
        // Находим ордера, которые нужно скопировать (ближайшие, но еще не скопированные)
        var ordersToCopy = allNearestOrders
            .Where(orderFills => !copyOrdersForPair.Any(co => co.OriginalOrderId == orderFills.OriginalOrder.OrderId))
            .ToArray();

        // Находим копируемые ордера, которые больше не являются ближайшими (нужно отменить)
        var ordersToCancel = copyOrdersForPair
            .Where(co => !allNearestOrders.Any(nearest => nearest.OriginalOrder.OrderId == co.OriginalOrderId))
            .ToArray();

        return (ordersToCopy, ordersToCancel);
    }

    private void OnNoneOrdersHandler(OriginalOrder[] orders)
    {
        if (orders.Length == 0)
            return;

        var orderIds = string.Join(", ", orders.Select(o => o.OrderId));
        _logger.LogWarning($"CopyOrderService2.OnNoneOrdersHandler: Получены ордера с типом None. Количество: {orders.Length}, OrderIds: [{orderIds}]");
    }
}
