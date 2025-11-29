using CopyTrading.Mappers;
using CopyTrading.Models.Models;
using CopyTrading.Repository.Influx.Interfaces;
using CopyTrading.Settings;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;

namespace CopyTrading.Repository.Influx;

public class CandlesRepository : ICandlesRepository
{
    private const string bucket = "Candles";
    private const string org = "CopyTrade";

    private readonly ILogger<TradeRepository> _logger;
    private readonly InfluxDBClient _client;

    public CandlesRepository(ILogger<TradeRepository> logger)
    {
        _logger = logger;
        _client = new InfluxDBClient(@"http://localhost:8086", InfluxSettings.token);
    }

    public void WriteCandles(Candle[] candles)
    {
        var measure = candles.Select(candle => candle.ToMeasurement()).ToList();

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