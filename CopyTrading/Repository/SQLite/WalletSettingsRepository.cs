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

    public async Task Upsert(CopyTradeWalletSettings settings)
    {
        const string sql = @"INSERT INTO WalletSettings (Wallet, ValueUsd, CopyKoef)
                VALUES (@Wallet, @ValueUsd, @CopyKoef)
                ON CONFLICT(Wallet) DO UPDATE SET
                    ValueUsd = excluded.ValueUsd,
                    CopyKoef = excluded.CopyKoef;";
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Wallet", settings.Wallet.Value);
            cmd.Parameters.AddWithValue("@ValueUsd", settings.VolumeUsd);
            cmd.Parameters.AddWithValue("@CopyKoef", settings.CopyKoef);

            var affected = await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("WalletSettingsRepository.Upsert affected={Affected} wallet={Wallet}", affected, settings.Wallet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WalletSettingsRepository.Upsert error for {Wallet}", settings.Wallet);
            throw;
        }
    }

    public async Task<CopyTradeWalletSettings?> Get(Wallet wallet)
    {
        const string sql = @"SELECT Wallet, ValueUsd, CopyKoef FROM WalletSettings WHERE Wallet = @Wallet LIMIT 1;";
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Wallet", wallet.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var valueUsdDouble = reader.GetDouble(reader.GetOrdinal("ValueUsd"));
                var copyKoefDouble = reader.GetDouble(reader.GetOrdinal("CopyKoef"));
                return new CopyTradeWalletSettings
                {
                    Wallet = wallet,
                    VolumeUsd = Convert.ToDecimal(valueUsdDouble),
                    CopyKoef = Convert.ToDecimal(copyKoefDouble)
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
        const string sql = @"SELECT Wallet, ValueUsd, CopyKoef FROM WalletSettings;";
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqliteCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync();

            var list = new List<CopyTradeWalletSettings>();
            while (await reader.ReadAsync())
            {
                var walletStr = reader.GetString(reader.GetOrdinal("Wallet"));
                var valueUsdDouble = reader.GetDouble(reader.GetOrdinal("ValueUsd"));
                var copyKoefDouble = reader.GetDouble(reader.GetOrdinal("CopyKoef"));

                list.Add(new CopyTradeWalletSettings
                {
                    Wallet = new Wallet(walletStr),
                    VolumeUsd = Convert.ToDecimal(valueUsdDouble),
                    CopyKoef = Convert.ToDecimal(copyKoefDouble)
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
