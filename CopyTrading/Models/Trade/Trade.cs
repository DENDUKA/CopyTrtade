using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Values;

namespace CopyTrading.Models.Trade;

public record Trade
{
    public long OrderId { get; set; }
    public long TradeId { get; set; }
    public string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal RealQuantity => Direction == Direction.Long ? Quantity : -Quantity;
    public decimal Price { get; set; }
    public DateTime TimeStamp { get; set; }
    public OrderSubType SubType { get; set; }
    public bool IsFuture { get; set; }
    public Direction Direction { get; set; }
    public decimal StartPosition { get; set; }
    public bool IsTaker { get; set; }
    public Wallet Wallet { get; set; }
    public decimal VolumeUsd => Quantity * Price;
}