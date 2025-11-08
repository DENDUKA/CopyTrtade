using CopyTrading.Models.Models;
using CopyTrading.Models.Values;
using CopyTrading.Settings;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

/// <summary>
/// Репозиторий для таблицы WalletSettings
/// Schema:
///   CREATE TABLE WalletSettings (
///       Wallet TEXT NOT NULL PRIMARY KEY,
///       ValueUsd REAL NOT NULL
///   );
/// </summary>
public class WalletSettingsRepository(ILogger<WalletSettingsRepository> _logger)
{
    private static string ConnectionString => $"Data Source={SQLLiteSettings.Path}";

    private async Task EnsureSchema()
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS WalletSettings (
Wallet TEXT NOT NULL PRIMARY KEY,
ValueUsd REAL NOT NULL
);";
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task Upsert(Wallet wallet, decimal valueUsd)
    {
        const string sql = @"
            INSERT INTO WalletSettings (Wallet, ValueUsd)
            VALUES (@Wallet, @ValueUsd)
            ON CONFLICT(Wallet) DO UPDATE SET
                ValueUsd = excluded.ValueUsd;";

        try
        {
            await EnsureSchema();
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Wallet", wallet.Value);
            cmd.Parameters.AddWithValue("@ValueUsd", valueUsd);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletSettingsRepository.UpsertAsync error for {Wallet}", wallet);
            throw;
        }
    }

    public async Task<CopyTradeWalletSettings?> Get(Wallet wallet)
    {
        const string sql = @"SELECT Wallet, ValueUsd FROM WalletSettings WHERE Wallet = @Wallet LIMIT 1;";

        try
        {
            await EnsureSchema();
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Wallet", wallet.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var valueUsdDouble = reader.GetDouble(reader.GetOrdinal("ValueUsd"));
                return new CopyTradeWalletSettings
                {
                    Wallet = wallet,
                    VolumeUsd = Convert.ToDecimal(valueUsdDouble)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletSettingsRepository.GetAsync error for {Wallet}", wallet);
            throw;
        }
    }

    public async Task<CopyTradeWalletSettings[]> GetAll()
    {
        const string sql = @"SELECT Wallet, ValueUsd FROM WalletSettings;";

        try
        {
            await EnsureSchema();
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<CopyTradeWalletSettings>();
            while (await reader.ReadAsync())
            {
                var walletStr = reader.GetString(reader.GetOrdinal("Wallet"));
                var valueUsdDouble = reader.GetDouble(reader.GetOrdinal("ValueUsd"));

                // Wallet ctor валидирует строку
                list.Add(new CopyTradeWalletSettings
                {
                    Wallet = new Wallet(walletStr),
                    VolumeUsd = Convert.ToDecimal(valueUsdDouble)
                });
            }

            return [.. list];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletSettingsRepository.GetAll error");
            throw;
        }
    }

    public async Task<bool> Delete(Wallet wallet)
    {
        const string sql = @"DELETE FROM WalletSettings WHERE Wallet = @Wallet;";
        try
        {
            await EnsureSchema();
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Wallet", wallet.Value);
            var affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletSettingsRepository.Delete error for {Wallet}", wallet);
            throw;
        }
    }
}
