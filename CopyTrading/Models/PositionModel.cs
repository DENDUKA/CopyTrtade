using CopyTrading.Models.Enums;

namespace CopyTrading.Models;

public record PositionModel
{
    public string Symbol { get; set; }
    public double? AverageEntryPrice { get; set; }
    public int? Leverage { get; internal set; }
    public double? LiquidationPrice { get; set; }
    public double? MarginUsage { get;  set; }
    public double? Quantity { get; set; }
    public double? ValueUsd { get; set; }
    public Direction Direction => Quantity < 0 ? Direction.Short : Direction.Long;

    public override string ToString()
    {
        return $"{Symbol}: Qty={Quantity}, Dir={Direction}, Entry={AverageEntryPrice}, Lev={Leverage}, Liq={LiquidationPrice}, Margin={MarginUsage}, Value={ValueUsd}";
    }
}
