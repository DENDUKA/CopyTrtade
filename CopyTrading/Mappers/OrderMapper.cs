using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.ProviderModels.InfluxDB;
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
            Time = DateTime.SpecifyKind(data.Order.Timestamp, DateTimeKind.Utc).ToLocalTime(),
            Status = data.Status.ToBll(),
            Wallet = wallet,
            Type = data.Order.OrderType
        };
    }

    public static OriginalOrder ToBll(this HyperLiquidOpenOrder data, Wallet wallet)
    {
        return new OriginalOrder()
        {
            OrderId = data.OrderId,
            Symbol = data.Symbol,
            Direction = data.OrderSide == HyperLiquid.Net.Enums.OrderSide.Buy ? Direction.Long : Direction.Short,
            Price = data.Price,
            Quantity = data.Quantity,
            Time = DateTime.SpecifyKind(data.Timestamp, DateTimeKind.Utc).ToLocalTime(),
            Status = Models.Models.Enums.Order.OrderStatus.Open, // Открытые ордера всегда имеют статус Open
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