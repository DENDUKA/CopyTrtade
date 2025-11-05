using CopyTrading.Settings;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class LogRepository(ILogger<OrderRepository> _logger)
{
    public async Task ClearAll()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={SQLLiteSettings.Path}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"DELETE FROM Logs ";

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"LogRepository ClearAll Error: {ex.Message}");
        }
    }
} 
