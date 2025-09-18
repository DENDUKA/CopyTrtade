using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class TradeRepository
{
    private readonly string _dbPath = Path.Combine(@"D:\Programs\ArbitrageExchangesScanes\CopyTrtade\SQLliteBD", "CopyTraidingDB.db");

    public async Task WriteMinPeForTrade(long tradeId, string wallet, double accountVolume, double volume, double minPE)
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MinPerpEquityForTrades
                    (TradeId, Wallet, WalletPerpEquity, TradeValue, MinPerpEquityForCopyTrade)
                VALUES
                    (@TradeId, @Wallet, @WalletPerpEquity, @TradeValue, @MinPerpEquityForCopyTrade)
                ON CONFLICT(TradeId) DO UPDATE SET
                    Wallet = excluded.Wallet,
                    WalletPerpEquity = excluded.WalletPerpEquity,
                    TradeValue = excluded.TradeValue,
                    MinPerpEquityForCopyTrade = excluded.MinPerpEquityForCopyTrade;
            ";

            command.Parameters.AddWithValue("@TradeId", tradeId);
            command.Parameters.AddWithValue("@Wallet", wallet);
            command.Parameters.AddWithValue("@WalletPerpEquity", accountVolume);
            command.Parameters.AddWithValue("@TradeValue", volume);
            command.Parameters.AddWithValue("@MinPerpEquityForCopyTrade", minPE);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing MinPerpEquityForTrade: {ex.Message}");
        }
    }
}
