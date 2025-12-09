using CopyTrading.Repository.SQLInterfaces.Interfaces;
using CopyTrading.Settings;
using Npgsql;

namespace CopyTrading.Repository.PostgreSQL;

public class LogRepository(ILogger<LogRepository> _logger, PostgreSQLSettings _settings) : ILogRepository
{
    public async Task ClearAll()
    {
        try
        {
            using var connection = new NpgsqlConnection(_settings.GetConnectionString());
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"DELETE FROM Logs";

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"LogRepository ClearAll Error: {ex.Message}");
        }
    }
}
