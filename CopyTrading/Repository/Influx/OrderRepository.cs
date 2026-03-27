using CopyTrading.Mappers;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Repository.InfluxInterfaces;
using CopyTrading.Settings;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;

namespace CopyTrading.Repository.Influx;

public class OrderRepository(ILogger<OrderRepository> logger) : IOrderRepository
{
    private readonly InfluxDBClient _client = new(InfluxSettings.Url, InfluxSettings.Token);

    public void WriteOrder(OriginalOrder[] orders)
    {
        var measure = orders.Select(order => order.ToMeasurement()).ToList();

        using var writeApi = _client.GetWriteApi();

        try
        {
            writeApi.WriteMeasurements(measure, WritePrecision.S, InfluxSettings.OrdersBucket, InfluxSettings.Org);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing order data to InfluxDB");
        }
    }
}
