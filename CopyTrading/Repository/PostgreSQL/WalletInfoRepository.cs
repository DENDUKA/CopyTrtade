using CopyTrading.Extensions;
using CopyTrading.Repository.SQLInterfaces.Interfaces;
using CopyTrading.Repository.SQLite.Dto;
using CopyTrading.Settings;
using Npgsql;

namespace CopyTrading.Repository.PostgreSQL;

public class WalletInfoRepository(ILogger<WalletInfoRepository> _logger, PostgreSQLSettings _settings) : IWalletInfoRepository
{
    public async Task WriteCurrentPositions(WalletSnapshotPositionsDto dto)
    {
        var query = @"INSERT INTO WalletSnapshotPositions (Wallet, DateTime, Positions)
                        VALUES (@wallet, @dateTime, @positions);";

        try
        {
            await using var connection = new NpgsqlConnection(_settings.GetConnectionString());
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@wallet", dto.Wallet.Value);
            command.Parameters.AddWithValue("@dateTime", dto.TimeStamp.ToStringMs());
            command.Parameters.AddWithValue("@positions", dto.Positions);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WalletInfoRepository WriteCurrentPositions: {ex.Message}");
        }
    }
}
