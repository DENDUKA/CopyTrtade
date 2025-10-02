using CopyTrading.Models.Enums.Order;

namespace CopyTrading.Models.Orders;

public class OriginalOrder : Order
{

    public DateTime Time { get; init; }
    public OrderStatus Status { get; init; }

    public override string ToString()
    {
        return $"{Wallet} {OrderId} {Time} {Symbol} {Direction} {Price} {Quantity} {Value} {SubType} {Status}";
    }
}