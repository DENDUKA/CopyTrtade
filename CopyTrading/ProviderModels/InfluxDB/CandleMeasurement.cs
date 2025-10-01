using InfluxDB.Client.Core;

namespace CopyTrading.ProviderModels.InfluxDB
{
    [Measurement("Candles")]
    public class CandleMeasurement
    {
        [Column("open")]  public decimal Open { get; set; }
        [Column("high")] public decimal High { get; set; }
        [Column("low")] public decimal Low { get; set; }
        [Column("close")] public decimal Close { get; set; }
        [Column("symbol", IsTag = true)] public string Symbol { get; set; }
        [Column("volume")] public decimal Volume { get; set; }
        [Column(IsTimestamp = true)] public DateTime Time { get; set; }
        [Column("interval")] public int Interval { get; set; }
    }
}