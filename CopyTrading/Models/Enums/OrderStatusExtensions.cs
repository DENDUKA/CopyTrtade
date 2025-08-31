namespace CopyTrading.Models.Enums;

public static class OrderStatusExtensions
{
    public static OrderStatus ToBll(this HyperLiquid.Net.Enums.OrderStatus status)
    {
        switch(status)
        {
            case HyperLiquid.Net.Enums.OrderStatus.Open: return OrderStatus.Open;
            case HyperLiquid.Net.Enums.OrderStatus.Filled: return OrderStatus.Filled;
            case HyperLiquid.Net.Enums.OrderStatus.Canceled: return OrderStatus.Canceled;
            case HyperLiquid.Net.Enums.OrderStatus.Triggered: return OrderStatus.Triggered;
            case HyperLiquid.Net.Enums.OrderStatus.Rejected: return OrderStatus.Rejected;
            case HyperLiquid.Net.Enums.OrderStatus.MarginCanceled: return OrderStatus.MarginCanceled;
            default:
            {
                Console.WriteLine($"Unknown order status: {status}");
                return OrderStatus.Unknown; // Можно выбрать любой дефолтный статус, например Open
            }
        };
    }
}