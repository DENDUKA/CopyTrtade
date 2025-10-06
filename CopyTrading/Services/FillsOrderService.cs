using CopyTrading.DataEvents;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CopyTrading.Services;

public class FillsOrderService
{
    private readonly ConcurrentDictionary<long, OrderFills> _orders = [];
    private readonly ConcurrentDictionary<long, OriginalTrade> _pendingTrades = [];
    private readonly ConcurrentDictionary<long, object> _orderLocks = new();

    private readonly ILogger<FillsOrderService> _logger;    

    public FillsOrderService(ILogger<FillsOrderService> logger)
    {
        _logger = logger;

        DataBusEvents.NewTrades += OnNewTrades;
        DataBusEvents.NewOrders += OnNewOrders;
    }

    private void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var newOrder in orders)
        {
            var lockObj = _orderLocks.GetOrAdd(newOrder.OrderId, _ => new object());
            lock (lockObj)
            {
                if (_orders.TryGetValue(newOrder.OrderId, out var orderFills))
                {
                    ChangeInOrder(newOrder, orderFills.OriginalOrder);
                }
                else
                {
                    var newOrderFills = new OrderFills(newOrder);
                    AddTradesFromPending(newOrderFills);

                    if (!_orders.TryAdd(newOrder.OrderId, newOrderFills))
                    {
                        _logger.LogError($"FillsOrderService OnNewOrders: не удалось добавить ордер {newOrder.OrderId}");
                    }
                }
            }
        }
    }

    private void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades)
    {
        if (trades.IsSnapshot) return;

        foreach (var trade in trades.Trades)
        {
            var lockObj = _orderLocks.GetOrAdd(trade.OrderId, _ => new object());
            lock (lockObj)
            {
                if (_orders.TryGetValue(trade.OrderId, out var orderFills))
                {
                    if (orderFills.Trades.FirstOrDefault(x => x.TradeId == trade.TradeId) is not null) continue;

                    orderFills.Trades.Add(trade);

                    _logger.LogInformation($"FillsOrderService OnNewTrades: ордер {trade.OrderId} fillStatus : {orderFills.FillStatus.ToString()}");
                }
                else
                {
                    _pendingTrades.TryAdd(trade.TradeId, trade);
                    _logger.LogError($"FillsOrderService OnNewTrades: не удалось найти ордер {trade.OrderId}");
                }
            }
        }
    }

    private void ChangeInOrder(OriginalOrder newOrder, OriginalOrder oldOrder)
    {
        string changes = string.Empty;

        // Сравнение всех свойств Order
        if (newOrder.OrderId != oldOrder.OrderId)
            changes += $"OrderId: {oldOrder.OrderId} -> {newOrder.OrderId};\n";
        if (newOrder.Wallet?.Value != oldOrder.Wallet?.Value)
            changes += $"Wallet: {oldOrder.Wallet?.Value} -> {newOrder.Wallet?.Value};\n";
        if (newOrder.Symbol != oldOrder.Symbol)
            changes += $"Symbol: {oldOrder.Symbol} -> {newOrder.Symbol};\n";
        if (newOrder.Price != oldOrder.Price)
            changes += $"Price: {oldOrder.Price} -> {newOrder.Price};\n";
        if (newOrder.Quantity != oldOrder.Quantity)
            changes += $"Size: {oldOrder.Quantity} -> {newOrder.Quantity};\n";
        if (newOrder.Leverage != oldOrder.Leverage)
            changes += $"Leverage: {oldOrder.Leverage} -> {newOrder.Leverage};\n";
        if (newOrder.SubType != oldOrder.SubType)
            changes += $"SubType: {oldOrder.SubType} -> {newOrder.SubType};\n";
        if (newOrder.Direction != oldOrder.Direction)
            changes += $"Direction: {oldOrder.Direction} -> {newOrder.Direction};\n";
        if (newOrder.VolumeUsd != oldOrder.VolumeUsd)
            changes += $"Value: {oldOrder.VolumeUsd} -> {newOrder.VolumeUsd};\n";
        if (newOrder.Status != oldOrder.Status)
            changes += $"Status: {oldOrder.Status} -> {newOrder.Status};\n";

        if (string.IsNullOrEmpty(changes))
        {
            _logger.LogInformation($"Изменения в ордере {newOrder.OrderId}: нет");
        }
        else
        {
            _logger.LogInformation($"Изменения в ордере {newOrder.OrderId}:\n{changes}");
        }
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
