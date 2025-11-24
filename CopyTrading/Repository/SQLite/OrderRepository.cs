using CopyTrading.Extensions;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Settings;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class OrderRepository(ILogger<OrderRepository> _logger) : IOrderRepository
{
    public async Task WriteOrder(
        OriginalOrder order)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={SQLLiteSettings.Path}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO Orders (
                OrderId, Wallet, Time, Symbol, Direction, Price, Size, Value, Status
            ) VALUES (
                @OrderId, @Wallet, @Time, @Symbol, @Direction, @Price, @Size, @Value, @Status
            )
            ON CONFLICT(OrderId) DO UPDATE SET Status = excluded.Status;";

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
            _logger.LogError($"Repository WriteMinPeForTrade: {ex.Message}");
        }
    }
}