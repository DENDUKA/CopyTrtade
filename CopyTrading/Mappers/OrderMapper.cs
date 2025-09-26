using CopyTrading.Mappers;
using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using CopyTrading.ProviderModels.InfluxDB;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class OrderMapper
{
    public static OriginalOrder ToBll(this HyperLiquidOrderStatus data, string wallet)
    {
        return new OriginalOrder()
        {
            OrderId = data.Order.OrderId,
            Coin = data.Order.Symbol,
            Direction = data.Order.OrderSide == HyperLiquid.Net.Enums.OrderSide.Buy ? Direction.Long : Direction.Short,
            Price = (double)data.Order.Price,
            Size = (double)data.Order.Quantity,
            Time = data.Order.Timestamp,
            Status = data.Status.ToBll(),
            Wallet = wallet
        };
    }

    public static CopiedOrderMeasurement ToMeasurement(this OriginalOrder order)
    {
        return new CopiedOrderMeasurement
        {
            Id = order.OrderId,
            Wallet = order.Wallet,
            Coin = order.Coin,
            Size = order.Size,
            Price = order.Price,
            Value = order.Value,
            Status = order.Status.ToString(),
            Direction = order.Direction.ToString(),
            OrderType = order.SubType.ToString(),
            Time = order.Time,
        };
    }

    public static Direction ToBll(this HyperLiquid.Net.Enums.OrderSide side)
    {
        return side == HyperLiquid.Net.Enums.OrderSide.Sell ? Direction.Short : Direction.Long;
    }

    private static Direction ToDirection(string direction)
    {
        return direction == "B" ? Direction.Long : Direction.Short;
    }

}