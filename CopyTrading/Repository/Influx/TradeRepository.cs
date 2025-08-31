using CopyTrading.Mappers;
using CopyTrading.Models.Trade;
using CopyTrading.Settings;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;

namespace CopyTrading.Repository.Influx;

public class TradeRepository
{
    private const string bucket = "Trades";
    private const string org = "CopyTrade";

    private readonly ILogger<TradeRepository> _logger;
    private readonly InfluxDBClient _client;

    public TradeRepository(ILogger<TradeRepository> logger)
    {
        _logger = logger;

        _client = new InfluxDBClient(@"http://localhost:8086", InfluxSettings.token);
    }

    public void WriteTrades(OriginalTrade[] trades)
    {
        var measure = trades.Select(trade => trade.ToMeasurement()).ToList();

        using var writeApi = _client.GetWriteApi();

        try
        {
            writeApi.WriteMeasurements(measure, WritePrecision.S, bucket, org);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing order data to InfluxDB");
        }
    }
}
