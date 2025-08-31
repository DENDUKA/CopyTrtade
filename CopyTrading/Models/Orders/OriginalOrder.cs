using CopyTrading.Models.Enums;

namespace CopyTrading.Models.Orders;

public class OriginalOrder : Order
{

    public DateTime Time { get; init; }
    public OrderStatus Status { get; init; }

    public override string ToString()
    {
        return $"{Wallet} {Id} {Time} {Coin} {Direction} {Price} {Size} {Value} {OrderType} {Status}";
    }
}