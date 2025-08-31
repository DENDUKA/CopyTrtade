using CopyTrading.Models.Enums;
using CopyTrading.Models.Orders;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Providers.InMemory;

public class OrderProvider : IOrdersProvider
{
    private double _ballance = 1000;

    private readonly ConcurrentDictionary<long, CopyOrder> PlacedOrders = new();
    private readonly ConcurrentDictionary<long, CopyOrder> FilledOrders = new();

    public async Task<OrderPlaceResult> Place(CopyOrder order)
    {
        var value = order.Price * order.Size;

        if (value < 10) return OrderPlaceResult.Minimum10Dollrs;

        if(_ballance - value / order.Leverage < 0 ) return OrderPlaceResult.LowBallance;

        AddPlaceOrder(order);

        return OrderPlaceResult.Ok;
    }

    public async Task<OrderPlaceResult> Close(long orderId)
    {
        if (PlacedOrders.TryGetValue(orderId, out var order))
        {
            PlacedOrders.Remove(orderId, out var _);
            _ballance += order.Price * order.Size;
        }
        else
        {
            return OrderPlaceResult.OrderNotFound;
        }

        return OrderPlaceResult.Ok;
    }

    public async Task<OrderPlaceResult> Filled(long orderId)
    {
        if (PlacedOrders.TryGetValue(orderId, out var order))
        {
            PlacedOrders.Remove(orderId, out var _);
            FilledOrders.TryAdd(orderId, order);
        }
        else
        {
            return OrderPlaceResult.OrderNotFound;
        }

        return OrderPlaceResult.Ok;
    }

    private void AddPlaceOrder(CopyOrder order)
    {
        PlacedOrders.TryAdd(order.Id, order);
        _ballance -= order.Price * order.Size;
    }
}
