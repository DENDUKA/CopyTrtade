using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;

namespace CopyTrading.Models.Orders;

public abstract class Order
{
    public long OrderId { get; set; }
    public string Wallet { get; set; }
    public string Coin { get; set; }
    public double Price { get; set; }
    public double Size { get; set; }
    public double Leverage { get; set; }
    public OrderSubType SubType { get; set; }
    public Direction Direction { get; set; }
    public double Value => Price * Size;
}
