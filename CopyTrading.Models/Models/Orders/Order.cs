using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models.Orders;

public abstract class Order
{
    public long OrderId { get; set; }
    public Wallet Wallet { get; set; }
    public string Symbol { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal RealQuantity => Direction == Direction.Long ? Quantity : -Quantity;
    public decimal Leverage { get; set; }
    public OrderSubType SubType { get; set; }
    public Direction Direction { get; set; }
    public decimal VolumeUsd => Price * Quantity;
}
