using CopyTrading.Extensions;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Repository.SQLInterfaces.Interfaces;
using CopyTrading.Settings;
using Npgsql;

namespace CopyTrading.Repository.PostgreSQL;

public class OrderRepository(ILogger<OrderRepository> _logger, PostgreSQLSettings _settings) : IOrderRepository
{
    public async Task WriteOrder(OriginalOrder order)
    {
        try
        {
            using var connection = new NpgsqlConnection(_settings.GetConnectionString());
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Orders (
                    OrderId, Wallet, Time, Symbol, Direction, Price, Size, Value, Status
                ) VALUES (
                    @OrderId, @Wallet, @Time, @Symbol, @Direction, @Price, @Size, @Value, @Status
                )
                ON CONFLICT(OrderId) DO UPDATE SET Status = EXCLUDED.Status;";

            command.Parameters.AddWithValue("@OrderId", order.OrderId);
            command.Parameters.AddWithValue("@Wallet", order.Wallet?.ToString() ?? string.Empty);
            command.Parameters.AddWithValue("@Time", order.Time.ToStringMs());
            command.Parameters.AddWithValue("@Symbol", order.Symbol);
            command.Parameters.AddWithValue("@Direction", order.Direction.ToString());
            command.Parameters.AddWithValue("@Price", order.Price);
            command.Parameters.AddWithValue("@Size", order.Quantity);
            command.Parameters.AddWithValue("@Value", order.VolumeUsd);
            command.Parameters.AddWithValue("@Status", order.Status.ToString());

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteOrder: {ex.Message}");
        }
    }
}
