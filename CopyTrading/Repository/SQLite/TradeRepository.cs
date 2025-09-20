using CopyTrading.Repository.SQLite.Dto;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class TradeRepository
{
    private readonly string _dbPath = Path.Combine(@"D:\Programs\ArbitrageExchangesScanes\CopyTrtade\SQLliteBD", "CopyTraidingDB.db");

    public async Task WriteMinPeForTrade(MinPEForTradeDto dto)
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MinPerpEquityForTrades
                    (TradeId, Wallet, WalletPerpEquity, TradeValue, MinPerpEquityForCopyTrade, Spread, DeltaTimeS, Time, Symbol)
                VALUES
                    (@TradeId, @Wallet, @WalletPerpEquity, @TradeValue, @MinPerpEquityForCopyTrade, @Spread, @DeltaTimeS, @Time, @Symbol)
                ON CONFLICT(TradeId) DO UPDATE SET
                    Wallet = excluded.Wallet,
                    WalletPerpEquity = excluded.WalletPerpEquity,
                    TradeValue = excluded.TradeValue,
                    MinPerpEquityForCopyTrade = excluded.MinPerpEquityForCopyTrade,
                    Spread = excluded.Spread,
                    DeltaTimeS = excluded.DeltaTimeS;
                    Time = excluded.Time,
                    Symbol = excluded.Symbol;
            ";

            command.Parameters.AddWithValue("@TradeId", dto.TradeId);
            command.Parameters.AddWithValue("@Wallet", dto.Wallet);
            command.Parameters.AddWithValue("@WalletPerpEquity", dto.AccountVolume);
            command.Parameters.AddWithValue("@TradeValue", dto.Volume);
            command.Parameters.AddWithValue("@MinPerpEquityForCopyTrade", dto.MinPE);
            command.Parameters.AddWithValue("@Spread", dto.Spread);
            command.Parameters.AddWithValue("@DeltaTimeS", dto.DeltaTimeS);
            command.Parameters.AddWithValue("@Time", dto.Time.ToString());
            command.Parameters.AddWithValue("@Symbol", dto.Symbol);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing MinPerpEquityForTrade: {ex.Message}");
        }
    }
}
