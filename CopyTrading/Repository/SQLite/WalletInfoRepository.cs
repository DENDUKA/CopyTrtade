using CopyTrading.Extensions;
using CopyTrading.Repository.SQLite.Dto;
using CopyTrading.Settings;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class WalletInfoRepository(ILogger<WalletInfoRepository> _logger) : IWalletInfoRepository
{
    public async Task WriteCurrentPositions(WalletSnapshotPositionsDto dto)
    {
        var query = @"INSERT INTO WalletSnapshotPositions (Wallet, DateTime, Positions) 
                        VALUES (@wallet, @dateTime, @positions);";                      

        try
        {
            await using var connection = new SqliteConnection($"Data Source={SQLLiteSettings.Path}");
            await connection.OpenAsync();

            await using var command = new SqliteCommand(query, connection);
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