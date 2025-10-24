using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

public class FillsOrderService
{
    private readonly ConcurrentDictionary<long, OrderFills> _orders = [];
    private readonly ConcurrentDictionary<long, OriginalTrade> _pendingTrades = [];

    internal readonly ConcurrentDictionary<long, string> _ordersWithError = [];

    private readonly ILogger<FillsOrderService> _logger;

    private readonly OrderStatus[] _orderFinalStatuses = [OrderStatus.Canceled, OrderStatus.Filled, OrderStatus.Rejected];

    public FillsOrderService(ILogger<FillsOrderService> logger)
    {
        _logger = logger;
    }

    public OrderFills[] GetAllOrderFills()
    {
        return _orders.Values.ToArray();
    }

    public void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var newOrder in orders)
        {
            if (_orders.TryGetValue(newOrder.OrderId, out var orderFills))
            {
                var orderChanges = GetChangesInOrder(newOrder, orderFills.OriginalOrder);
                orderFills.UpdateOrder(newOrder);
            }
            else
            {
                var newOrderFills = new OrderFills(newOrder);                

                if (!_orders.TryAdd(newOrder.OrderId, newOrderFills))
                {
                    _logger.LogError($"FillsOrderService OnNewOrders: не удалось добавить ордер {newOrder.OrderId}");
                }

                AddTradesFromPending(newOrderFills);
            }

            OnOrderFinished(newOrder);
        }
    }

    public void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades)
    {
        if (trades.IsSnapshot) return;

        foreach (var trade in trades.Trades)
        {
            _logger.LogInformation($"FillsOrderService OnNewTrades: Новый trade {trade.ToString()}");

            if (_orders.TryGetValue(trade.OrderId, out var orderFills))
            {
                if (orderFills.Trades.FirstOrDefault(x => x.TradeId == trade.TradeId) is not null) continue;

                orderFills.Trades.Add(trade);

                _logger.LogInformation($"FillsOrderService OnNewTrades: ордер {trade.OrderId} fillStatus : {orderFills.FillStatus.ToString()}");

                OnOrderFinished(orderFills.OriginalOrder);
            }
            else
            {
                _pendingTrades.TryAdd(trade.TradeId, trade);
                _logger.LogWarning($"FillsOrderService OnNewTrades: не удалось найти ордер {trade.OrderId}");
            }
        }
    }

    private void OnOrderFinished(OriginalOrder order)
    {
        _logger.LogInformation($"OnOrderFinished {order.OrderId} status: {order.Status}");

        if (!_orderFinalStatuses.Contains(order.Status)) return;

        if (order.Status == OrderStatus.Filled)
        {
            _orders.TryGetValue(order.OrderId, out var orderFills);

            if (orderFills.FilledQuantity == order.Quantity)//Успех. весь Order заполнен/выполнен. соответствует статусу
            {
                DataBusEvents.OrderFinished?.Invoke(orderFills);
                _logger.LogInformation($"OnOrderFinished Filled {order.OrderId} Success");
                _ordersWithError.Remove(order.OrderId, out var _);
            }
            else
            {
                string error = $"OnOrderFinished Filled {order.OrderId} не сходится сумма всех trades и order.Quantity";
                _logger.LogError(error);
                _ordersWithError.TryAdd(order.OrderId, error);
            }
            return;
        }

        if (order.Status == OrderStatus.Canceled)
        {
            _orders.TryGetValue(order.OrderId, out var orderFills);

            if (orderFills.FilledQuantity == 0)//Закрытие Order. В Order не был выполнен 
            {
                DataBusEvents.OrderFinished?.Invoke(orderFills);
                _logger.LogInformation($"OnOrderFinished Canceled {order.OrderId} Success");
                _ordersWithError.Remove(order.OrderId, out var _);
            }
            else //Закрытие Order. В Order был выполнен частично
            {
                DataBusEvents.OrderFinished?.Invoke(orderFills);
                _logger.LogWarning($"OnOrderFinished Canceled {order.OrderId} Order был выполнен частично");
                _ordersWithError.Remove(order.OrderId, out var _);
            }
            return;
        }

        _logger.LogWarning($"Неизвестная ошибка OrderId: {order.OrderId}");
        _ordersWithError.TryAdd(order.OrderId, $"Неизвестная ошибка OrderId: {order.OrderId}");
    }

    private OrderChanges GetChangesInOrder(OriginalOrder newOrder, OriginalOrder oldOrder)
    {
        var changes = OrderChanges.None;
        string changesString = string.Empty;

        // Сравнение всех свойств Order
        if (newOrder.OrderId != oldOrder.OrderId)
            changesString += $"OrderId: {oldOrder.OrderId} -> {newOrder.OrderId};\n";
        if (newOrder.Wallet?.Value != oldOrder.Wallet?.Value)
            changesString += $"Wallet: {oldOrder.Wallet?.Value} -> {newOrder.Wallet?.Value};\n";
        if (newOrder.Symbol != oldOrder.Symbol)
            changesString += $"Symbol: {oldOrder.Symbol} -> {newOrder.Symbol};\n";
        if (newOrder.Price != oldOrder.Price)
            changesString += $"Price: {oldOrder.Price} -> {newOrder.Price};\n";
        if (newOrder.Quantity != oldOrder.Quantity)
            changesString += $"Size: {oldOrder.Quantity} -> {newOrder.Quantity};\n";
        if (newOrder.Leverage != oldOrder.Leverage)
            changesString += $"Leverage: {oldOrder.Leverage} -> {newOrder.Leverage};\n";
        if (newOrder.SubType != oldOrder.SubType)
            changesString += $"SubType: {oldOrder.SubType} -> {newOrder.SubType};\n";
        if (newOrder.Direction != oldOrder.Direction)
        {
            changesString += $"Direction: {oldOrder.Direction} -> {newOrder.Direction};\n";
            changes |= OrderChanges.Direction;
        }
        if (newOrder.VolumeUsd != oldOrder.VolumeUsd)
            changesString += $"Value: {oldOrder.VolumeUsd} -> {newOrder.VolumeUsd};\n";
        if (newOrder.Status != oldOrder.Status)
        {
            changesString += $"Status: {oldOrder.Status} -> {newOrder.Status};\n";
            changes |= OrderChanges.Status;
        }

            if (string.IsNullOrEmpty(changesString))
        {
            _logger.LogInformation($"Изменения в ордере {newOrder.OrderId}: нет");
        }
        else
        {
            _logger.LogInformation($"Изменения в ордере {newOrder.OrderId}:\n{changesString}");
        }

        return changes;
    }

    private void AddTradesFromPending(OrderFills newOrderFills)
    {
        foreach (var pendingTrade in _pendingTrades.Values.Where(t => t.OrderId == newOrderFills.OriginalOrder.OrderId))
        {
            newOrderFills.Trades.Add(pendingTrade);
            _pendingTrades.TryRemove(pendingTrade.TradeId, out _);
        }
    }
}
