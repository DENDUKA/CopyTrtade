using CopyTrading.Mappers;
using CopyTrading.Models.Models;
using CopyTrading.Repository.InfluxInterfaces;
using CopyTrading.Settings;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;

namespace CopyTrading.Repository.Influx;

public class CandlesRepository(ILogger<TradeRepository> logger) : ICandlesRepository
{
    private readonly InfluxDBClient _client = new(InfluxSettings.Url, InfluxSettings.Token);

    public void WriteCandles(Candle[] candles)
    {
        var measure = candles.Select(candle => candle.ToMeasurement()).ToList();

        using var writeApi = _client.GetWriteApi();

        try
        {
            writeApi.WriteMeasurements(measure, WritePrecision.S, InfluxSettings.CandlesBucket, InfluxSettings.Org);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing order data to InfluxDB");
        }
    }
}
