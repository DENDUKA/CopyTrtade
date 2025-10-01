using InfluxDB.Client.Core;

namespace CopyTrading.ProviderModels.InfluxDB
{
    [Measurement("HistoryTrades")]
    public class TradeMeasurement
    {
        [Column("id")] public long Id { get; set; }
        [Column("orderid")] public long OrderId { get; set; }
        [Column("wallet", IsTag = true)] public string Wallet { get; set; }
        [Column("coin", IsTag = true)] public string Symbol { get; set; }
        [Column("size")] public decimal Size { get; set; }
        [Column("price")] public decimal Price { get; set; }
        [Column("value")] public decimal Value { get; set; }
        [Column("direction")] public string Direction { get; set; }
        [Column("ordertype")] public string Type { get; set; }
        [Column(IsTimestamp = true)] public DateTime Time { get; set; }
        [Column("isfuture")] public bool IsFutures { get; set; }
    }
}