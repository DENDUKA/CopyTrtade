using CopyTrading.Models.Enums.Order;

namespace CopyTrading.Models.Trade;

public class MinPEForTrade
{
    public long TradeId { get; set; }
    public decimal AccountVolume { get; set; }
    public decimal MinPE { get; set; }
    public decimal Spread { get; set; }
    public decimal DeltaTimeS { get; set; }
    public OrderSubType SubType { get; set; }
}