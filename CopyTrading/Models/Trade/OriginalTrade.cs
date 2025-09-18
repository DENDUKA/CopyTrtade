using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;

namespace CopyTrading.Models.Trade;

public record OriginalTrade
{
    public long OrderId { get; set; }
    public long TradeId { get; set; }
    public string Coin { get; set; }
    public double Quantity { get; set; }
    public double Price { get; set; }
    public DateTime TimeStamp { get; set; }
    public OrderSubType SubType { get; set; }
    public bool IsFuture { get; set; }
    public Direction Direction { get; set; }
    public double StartPosition { get; set; }
    public bool IsTaker { get; set; }
    public string Wallet { get; set; }
    public double Volume => Quantity * Price;
}