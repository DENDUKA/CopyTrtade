using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Values;

namespace CopyTrading.Models.Orders;

public abstract class Order
{
    public long OrderId { get; set; }
    public Wallet Wallet { get; set; }
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Leverage { get; set; }
    public OrderSubType SubType { get; set; }
    public Direction Direction { get; set; }
    public decimal Value => Price * Quantity;
}
