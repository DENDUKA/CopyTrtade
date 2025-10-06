using CopyTrading.Models.Enums;
using CopyTrading.Models.Orders;
using CopyTrading.ProviderModels.InfluxDB;
using CopyTrading.Values;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class OrderMapper
{
    public static OriginalOrder ToBll(this HyperLiquidOrderStatus data, Wallet wallet)
    {
        return new OriginalOrder()
        {
            OrderId = data.Order.OrderId,
            Symbol = data.Order.Symbol,
            Direction = data.Order.OrderSide == HyperLiquid.Net.Enums.OrderSide.Buy ? Direction.Long : Direction.Short,
            Price = data.Order.Price,
            Quantity = data.Order.Quantity,
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
            Wallet = order.Wallet.Value,
            Symbol = order.Symbol,
            Size = order.Quantity,
            Price = order.Price,
            Value = order.VolumeUsd,
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