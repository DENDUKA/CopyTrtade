using CopyTrading.Models.Models.Enums.Order;

namespace CopyTrading.Models.Models.Orders;

public class OriginalOrder : Order
{

    public DateTime Time { get; set; }
    public OrderStatus Status { get; set; }
    public OrderType Type { get; set; }

    public override string ToString()
    {
        return $"{Wallet} {OrderId} {Time} {Symbol} {Direction} {Price} {Quantity} {VolumeUsd} {SubType} {Status}";
    }
}