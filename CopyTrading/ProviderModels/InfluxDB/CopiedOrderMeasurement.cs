using InfluxDB.Client.Core;

namespace CopyTrading.ProviderModels.InfluxDB
{
    [Measurement("copiedorder")]
    public class CopiedOrderMeasurement
    {
        [Column("id")] public long Id { get; set; }
        [Column("wallet", IsTag = true)] public string Wallet { get; set; }
        [Column("coin", IsTag = true)] public string Coin { get; set; }
        [Column("size")] public double Size { get; set; }
        [Column("price")] public double Price { get; set; }
        [Column("value")] public double Value { get; set; }
        [Column("status", IsTag = true)] public string Status { get; set; }
        [Column("direction")] public string Direction { get; set; }
        [Column("ordertype")] public string OrderType { get; set; }
        [Column(IsTimestamp = true)] public DateTime Time { get; set; }
    }
}