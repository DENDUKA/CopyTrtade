using CopyTrading.Models.Enums;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Trade;
using CopyTrading.Repository.SQLite.Dto;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class TradeRepository(ILogger<TradeRepository> _logger)
{
    private readonly string _dbPath = Path.Combine(@"D:\Programs\ArbitrageExchangesScanes\CopyTrtade\SQLliteBD", "CopyTraidingDB.db");

    public async Task WriteTrade(TradeModel trade)
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Trades
                    (TradeId, Wallet, TradeVolume, Symbol, Time, OrderId, Direction, Price, Quantity)
                VALUES
                    (@TradeId, @Wallet, @TradeVolume, @Symbol, @Time, @OrderId, @Direction, @Price, @Quantity)
                ON CONFLICT(TradeId) DO NOTHING; 
            ";

            command.Parameters.AddWithValue("@TradeId", trade.TradeId);
            command.Parameters.AddWithValue("@Wallet", trade.Wallet);
            command.Parameters.AddWithValue("@TradeVolume", trade.Volume);
            command.Parameters.AddWithValue("@Price", trade.Price);
            command.Parameters.AddWithValue("@Quantity", trade.Quantity);
            command.Parameters.AddWithValue("@Symbol", trade.Coin);
            command.Parameters.AddWithValue("@Time", trade.TimeStamp.ToString());
            command.Parameters.AddWithValue("@OrderId", trade.OrderId);
            command.Parameters.AddWithValue("@Direction", trade.Direction.ToString());

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteTrade: {ex.Message}");
        }
    }

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
                    (TradeId, Wallet, WalletPerpEquity, TradeValue, MinPerpEquityForCopyTrade, Spread, DeltaTimeS, Time, Symbol, OrderId, Direction, SubType)
                VALUES
                    (@TradeId, @Wallet, @WalletPerpEquity, @TradeValue, @MinPerpEquityForCopyTrade, @Spread, @DeltaTimeS, @Time, @Symbol, @OrderId, @Direction, @SubType)
                ON CONFLICT(TradeId) DO UPDATE SET
                    Wallet = excluded.Wallet,
                    WalletPerpEquity = excluded.WalletPerpEquity,
                    TradeValue = excluded.TradeValue,
                    MinPerpEquityForCopyTrade = excluded.MinPerpEquityForCopyTrade,
                    Spread = excluded.Spread,
                    DeltaTimeS = excluded.DeltaTimeS,
                    Time = excluded.Time,
                    Symbol = excluded.Symbol,
                    OrderId = excluded.OrderId,
                    Direction = excluded.Direction,
                    SubType = CASE WHEN excluded.SubType <> 'None' THEN excluded.SubType ELSE SubType END;
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
            command.Parameters.AddWithValue("@OrderId", dto.OrderId);
            command.Parameters.AddWithValue("@Direction", dto.Direction.ToString());
            command.Parameters.AddWithValue("@SubType", dto.SubType.ToString());

            var res =  await command.ExecuteNonQueryAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteMinPeForTrade: {ex.Message}");
        }
    }

    public async Task WriteMinPeForTrade(MinPEForTradeDto[] dtos)
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            foreach (var dto in dtos)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO MinPerpEquityForTrades
                        (TradeId, Wallet, WalletPerpEquity, TradeValue, MinPerpEquityForCopyTrade, Spread, DeltaTimeS, Time, Symbol, OrderId, Direction, SubType)
                    VALUES
                        (@TradeId, @Wallet, @WalletPerpEquity, @TradeValue, @MinPerpEquityForCopyTrade, @Spread, @DeltaTimeS, @Time, @Symbol, @OrderId, @Direction, @SubType)
                    ON CONFLICT(TradeId) DO UPDATE SET
                        Wallet = excluded.Wallet,
                        WalletPerpEquity = excluded.WalletPerpEquity,
                        TradeValue = excluded.TradeValue,
                        MinPerpEquityForCopyTrade = excluded.MinPerpEquityForCopyTrade,
                        Spread = excluded.Spread,
                        DeltaTimeS = excluded.DeltaTimeS,
                        Time = excluded.Time,
                        Symbol = excluded.Symbol,
                        OrderId = excluded.OrderId,
                        Direction = excluded.Direction,
                        SubType = CASE WHEN excluded.SubType <> 'None' THEN excluded.SubType ELSE SubType END;
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
                command.Parameters.AddWithValue("@OrderId", dto.OrderId);
                command.Parameters.AddWithValue("@Direction", dto.Direction.ToString());
                command.Parameters.AddWithValue("@SubType", dto.SubType.ToString());


                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteMinPeForTrade: {ex.Message}");
        }
    }

    public async Task<MinPEForTradeDto[]> MinPerpEquityForTradesQuery(MinPerpEquityForTradesQuery minPerpEquityForTradesQuery)
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @$"
                SELECT * FROM MinPerpEquityForTrades
            ";

            if (minPerpEquityForTradesQuery.SubTypes is not null && minPerpEquityForTradesQuery.SubTypes.Length > 0)
            {
                command.CommandText += $@"
                    WHERE SubType IS NULL OR 
                    SubType IN({ string.Join(", ", minPerpEquityForTradesQuery.SubTypes.Select(x => $"'{x}'"))})";
            }

            var result = new List<MinPEForTradeDto>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new MinPEForTradeDto
                {
                    TradeId = reader.GetInt64(reader.GetOrdinal("TradeId")),
                    Symbol = reader.GetString(reader.GetOrdinal("Symbol")),
                    Wallet = reader.GetString(reader.GetOrdinal("Wallet")),
                    Time = DateTime.Parse(reader.GetString(reader.GetOrdinal("Time"))),
                    AccountVolume = reader.GetDouble(reader.GetOrdinal("WalletPerpEquity")),
                    Volume = reader.GetDouble(reader.GetOrdinal("TradeValue")),
                    MinPE = reader.GetDouble(reader.GetOrdinal("MinPerpEquityForCopyTrade")),
                    Spread = reader.GetDouble(reader.GetOrdinal("Spread")),
                    DeltaTimeS = reader.GetDouble(reader.GetOrdinal("DeltaTimeS")),
                    OrderId = reader.GetInt64(reader.GetOrdinal("OrderId")),
                    Direction = reader.IsDBNull(reader.GetOrdinal("Direction"))
                        ? Direction.None
                        : Enum.Parse<Direction>(reader.GetString(reader.GetOrdinal("Direction"))),
                    SubType = reader.IsDBNull(reader.GetOrdinal("SubType"))
                        ? OrderSubType.None
                        : Enum.Parse<OrderSubType>(reader.GetString(reader.GetOrdinal("SubType"))),
                });
            }
            return result.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository MinPerpEquityForTradesQuery: {ex.Message}");
        }

        return [];
    }
}
