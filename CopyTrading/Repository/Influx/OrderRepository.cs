using CopyTrading.Mappers;
using CopyTrading.Models.Orders;
using CopyTrading.Settings;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;

namespace CopyTrading.Repository.Influx;

public class OrderRepository
{
    private const string bucket = "Orders";
    private const string org = "CopyTrade";

    private readonly ILogger<OrderRepository> _logger;
    private readonly InfluxDBClient _client;
    private readonly WriteApi _writeApi;

    public OrderRepository(ILogger<OrderRepository> logger)
    {
        _logger = logger;

        _client = new InfluxDBClient(@"http://localhost:8086", InfluxSettings.token);
    }

    public void WriteOrder(OriginalOrder[] orders)
    {
        var measure = orders.Select(order => order.ToMeasurement()).ToList();

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
