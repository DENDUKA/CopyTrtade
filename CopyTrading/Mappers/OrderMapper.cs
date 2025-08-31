using CopyTrading.Mappers;
using CopyTrading.Models.Enums;
using CopyTrading.Models.Orders;
using CopyTrading.ProviderModels.FetchOrder;
using CopyTrading.ProviderModels.InfluxDB;
using CopyTrading.ProviderModels.WsOrder;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class OrderMapper
{
    public static OriginalOrder ToBll(this FetchOrderRootModel dto, string wallet)
    {
        return new OriginalOrder()
        {
            Coin = dto.order.coin,
            Direction = ToDirection(dto.order.side),
            //OrderType = dto.order.orderType,
            Price = double.Parse(dto.order.limitPx),
            Size = double.Parse(dto.order.origSz),
            Time = DateTimeOffset.FromUnixTimeMilliseconds(dto.order.timestamp).LocalDateTime,
            Status = dto.status,
            Wallet = wallet
        };
    }

    public static OriginalOrder ToBll(this WsOrderModel dto, string wallet)
    {
        return new OriginalOrder()
        {
            Id = dto.Order.Oid,
            Coin = dto.Order.Coin,
            Direction = ToDirection(dto.Order.Side),
            Price = double.Parse(dto.Order.LimitPx),
            Size = double.Parse(dto.Order.OrigSz),
            Time = DateTimeOffset.FromUnixTimeMilliseconds(dto.Order.Timestamp).LocalDateTime,
            Status = dto.Status,
            Wallet = wallet,
        };
    }

    public static OriginalOrder ToBll(this HyperLiquidOrderStatus data, string wallet)
    {
        return new OriginalOrder()
        {
            Id = data.Order.OrderId,
            Coin = data.Order.Symbol,
            Direction = data.Order.OrderSide == HyperLiquid.Net.Enums.OrderSide.Buy ? Direction.Long : Direction.Short,
            Price = (double)data.Order.Price,
            Size = (double)data.Order.Quantity,
            Time = data.Order.Timestamp,
            Status = data.Status.ToBll(),
            Wallet = wallet,
            OrderType = data.Order.OrderType.ToBll(),
        };
    }

    public static CopiedOrderMeasurement ToMeasurement(this OriginalOrder order)
    {
        return new CopiedOrderMeasurement
        {
            Id = order.Id,
            Wallet = order.Wallet,
            Coin = order.Coin,
            Size = order.Size,
            Price = order.Price,
            Value = order.Value,
            Status = order.Status.ToString(),
            Direction = order.Direction.ToString(),
            OrderType = order.OrderType.ToString(),
            Time = order.Time,
        };
    }

    private static Direction ToDirection(string direction)
    {
        return direction == "B" ? Direction.Long : Direction.Short;
    }
}