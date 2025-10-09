using CopyTrading.Models.Models.Enums.Order;

namespace CopyTrading.Mappers;

public static class OrderStatusMapper
{
    public static OrderStatus ToBll(this HyperLiquid.Net.Enums.OrderStatus orderStatus)
    {
        switch (orderStatus)
        {
            case HyperLiquid.Net.Enums.OrderStatus.Canceled: return OrderStatus.Canceled;
            case HyperLiquid.Net.Enums.OrderStatus.Filled: return OrderStatus.Filled;
            case HyperLiquid.Net.Enums.OrderStatus.Open: return OrderStatus.Open;
            case HyperLiquid.Net.Enums.OrderStatus.MarginCanceled: return OrderStatus.MarginCanceled;
            case HyperLiquid.Net.Enums.OrderStatus.Rejected: return OrderStatus.Rejected;
            case HyperLiquid.Net.Enums.OrderStatus.Triggered: return OrderStatus.Triggered;

            default: return OrderStatus.Unknown;
        }

    }
}
