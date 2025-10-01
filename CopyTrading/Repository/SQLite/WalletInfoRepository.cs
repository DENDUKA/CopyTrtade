using CopyTrading.Extensions;
using CopyTrading.Repository.SQLite.Dto;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class WalletInfoRepository(ILogger<WalletInfoRepository> _logger)
{
    private readonly string _dbPath = Path.Combine(@"D:\Programs\ArbitrageExchangesScanes\CopyTrtade\SQLliteBD", "CopyTraidingDB.db");

    public async Task WriteCurrentPositions(WalletSnapshotPositionsDto dto)
    {
        var query = @"INSERT INTO WalletSnapshotPositions (Wallet, DateTime, Positions) 
                        VALUES (@wallet, @dateTime, @positions);";                      

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
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