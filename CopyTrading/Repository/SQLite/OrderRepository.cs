
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class OrderRepository
{
    // Относительный путь к базе данных относительно корня проекта
    private readonly string _databasePath = @"C:\Users\DENDUKA\source\repos\CopyTrading\SQLliteBD\CopyTraidingDB.db";

    public OrderRepository()
    {

    }

    public async Task AddOrder(CopyOrder order, OrderStatus status)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};");
        await connection.OpenAsync();

        using var command = new SqliteCommand(@"
            INSERT INTO [Order]
            (CopiedOrderId, Wallet, Status, MyOrderId) 
            VALUES
            (@CopiedOrderId, @Wallet, @Status, @MyOrderId);", connection);

        command.Parameters.AddWithValue("@Wallet", order.Wallet);
        command.Parameters.AddWithValue("@Status", status.ToString());
        command.Parameters.AddWithValue("@CopiedOrderId", order.OrderId);
        command.Parameters.AddWithValue("@MyOrderId", 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveOrder(long id)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};");
        await connection.OpenAsync();

        using var command = new SqliteCommand(@"
            DELETE FROM [Order]
            WHERE CopiedOrderId = @CopiedOrderId;", connection);

        command.Parameters.AddWithValue("@CopiedOrderId", id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ChangeOrderStatus(long id, OrderStatus status)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};");
        await connection.OpenAsync();

        using var command = new SqliteCommand(@"
            UPDATE [Order]
            SET Status = @Status
            WHERE CopiedOrderId = @CopiedOrderId;", connection);

        command.Parameters.AddWithValue("@Status", status.ToString());
        command.Parameters.AddWithValue("@CopiedOrderId", id);

        await command.ExecuteNonQueryAsync();
    }
}