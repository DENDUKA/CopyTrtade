using InfluxDB.Client.Core;

namespace CopyTrading.ProviderModels.InfluxDB
{
    [Measurement("Candles")]
    public class CandleMeasurement
    {
        [Column("open")]  public double Open { get; set; }
        [Column("high")] public double High { get; set; }
        [Column("low")] public double Low { get; set; }
        [Column("close")] public double Close { get; set; }
        [Column("symbol", IsTag = true)] public string Symbol { get; set; }
        [Column("volume")] public double Volume { get; set; }
        [Column(IsTimestamp = true)] public DateTime Time { get; set; }
        [Column("interval")] public int Interval { get; set; }
    }
}