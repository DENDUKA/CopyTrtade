using CopyTrading.Models.Enums;

namespace CopyTrading.Models.Orders;

public abstract class Order
{
    public long Id { get; set; }
    public string Wallet { get; set; }
    public string Coin { get; set; }
    public double Price { get; set; }
    public double Size { get; set; }
    public double Leverage { get; set; }
    public OrderBuyType OrderType { get; set; }
    public Direction Direction { get; set; }
    public double Value => Price * Size;
}
