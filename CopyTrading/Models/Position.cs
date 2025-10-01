using CopyTrading.Models.Enums;

namespace CopyTrading.Models;

public record Position
{
    public string Symbol { get; set; }
    public decimal AverageEntryPrice { get; set; }
    public int Leverage { get; set; }
    public decimal? LiquidationPrice { get; set; }
    public decimal MarginUsage { get; set; }
    public decimal Quantity { get; set; }
    public decimal VolumeUsd { get; set; }
    public Direction Direction => Quantity < 0 ? Direction.Short : Direction.Long;

    public override string ToString()
    {
        return $"{Symbol}: Qty={Quantity}, Dir={Direction}, Entry={AverageEntryPrice}, Lev={Leverage}, Liq={LiquidationPrice}, Margin={MarginUsage}, Value={VolumeUsd}";
    }
}
