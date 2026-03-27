namespace CopyTrading.Settings;

public static class InfluxSettings
{
    public static string Url =>
        Environment.GetEnvironmentVariable("INFLUXDB_URL")
        ?? "http://localhost:8086";

    public static string Token =>
        Environment.GetEnvironmentVariable("INFLUXDB_TOKEN")
        ?? "CHANGE_ME_INFLUX_TOKEN";

    public static string Org =>
        Environment.GetEnvironmentVariable("INFLUXDB_ORG")
        ?? "CopyTrade";

    public static string OrdersBucket =>
        Environment.GetEnvironmentVariable("INFLUXDB_BUCKET_ORDERS")
        ?? "copytrading";

    public static string TradesBucket =>
        Environment.GetEnvironmentVariable("INFLUXDB_BUCKET_TRADES")
        ?? "copytrading";

    public static string CandlesBucket =>
        Environment.GetEnvironmentVariable("INFLUXDB_BUCKET_CANDLES")
        ?? "copytrading";
}
