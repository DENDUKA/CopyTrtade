using CopyTrading.Extensions;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Repository.SQLInterfaces.Interfaces;
using CopyTrading.Repository.SQLite.Dto;
using CopyTrading.Settings;
using Microsoft.Data.Sqlite;

namespace CopyTrading.Repository.SQLite;

public class TradeRepository(ILogger<TradeRepository> _logger) : ITradeRepository
{
    private readonly string _databasePath = SQLLiteSettings.Path;

    public async Task WriteTrade(OriginalTrade trade)
    {
        try
        {
            var connectionString = $"Data Source={_databasePath}";
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
            command.Parameters.AddWithValue("@Wallet", trade.Wallet.Value);
            command.Parameters.AddWithValue("@TradeVolume", trade.VolumeUsd);
            command.Parameters.AddWithValue("@Price", trade.Price);
            command.Parameters.AddWithValue("@Quantity", trade.Quantity);
            command.Parameters.AddWithValue("@Symbol", trade.Symbol);
            command.Parameters.AddWithValue("@Time", trade.TimeStamp.ToStringMs());
            command.Parameters.AddWithValue("@OrderId", trade.OrderId);
            command.Parameters.AddWithValue("@Direction", trade.Direction.ToString());

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteTrade: {ex.Message}");
        }
    }

    public async Task WriteMinPeForTrade(MinPEForTrade dto)
    {
        try
        {
            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MinPerpEquityForTrades
                    (TradeId, WalletPerpEquity, MinPerpEquityForCopyTrade, Spread, DeltaTimeS, SubType)
                VALUES
                    (@TradeId, @WalletPerpEquity, @MinPerpEquityForCopyTrade, @Spread, @DeltaTimeS, @SubType)
                ON CONFLICT(TradeId) DO UPDATE SET
                    SubType = CASE WHEN excluded.SubType <> 'None' THEN excluded.SubType ELSE SubType END;
            ";

            command.Parameters.AddWithValue("@TradeId", dto.TradeId);
            command.Parameters.AddWithValue("@WalletPerpEquity", dto.AccountVolume);
            command.Parameters.AddWithValue("@MinPerpEquityForCopyTrade", dto.MinPE);
            command.Parameters.AddWithValue("@Spread", dto.Spread);
            command.Parameters.AddWithValue("@DeltaTimeS", dto.DeltaTimeS);
            command.Parameters.AddWithValue("@SubType", dto.SubType.ToString());

            var res =  await command.ExecuteNonQueryAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteMinPeForTrade: {ex.Message}");
        }
    }

    public async Task WriteMinPeForTrade(MinPEForTrade[] dtos)
    {
        try
        {
            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            foreach (var dto in dtos)
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO MinPerpEquityForTrades
                        (TradeId, WalletPerpEquity, MinPerpEquityForCopyTrade, Spread, DeltaTimeS, SubType)
                    VALUES
                        (@TradeId, , @WalletPerpEquity, @MinPerpEquityForCopyTrade, @Spread, @DeltaTimeS, @SubType)
                    ON CONFLICT(TradeId) DO UPDATE SET
                        WalletPerpEquity = excluded.WalletPerpEquity,
                        MinPerpEquityForCopyTrade = excluded.MinPerpEquityForCopyTrade,
                        Spread = excluded.Spread,
                        DeltaTimeS = excluded.DeltaTimeS,
                        SubType = CASE WHEN excluded.SubType <> 'None' THEN excluded.SubType ELSE SubType END;
                ";

                command.Parameters.AddWithValue("@TradeId", dto.TradeId);
                command.Parameters.AddWithValue("@WalletPerpEquity", dto.AccountVolume);
                command.Parameters.AddWithValue("@MinPerpEquityForCopyTrade", dto.MinPE);
                command.Parameters.AddWithValue("@Spread", dto.Spread);
                command.Parameters.AddWithValue("@DeltaTimeS", dto.DeltaTimeS);
                command.Parameters.AddWithValue("@SubType", dto.SubType.ToString());


                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteMinPeForTrade: {ex.Message}");
        }
    }

    public async Task<MinPEForTrade[]> MinPerpEquityForTradesQuery(MinPerpEquityForTradesQuery minPerpEquityForTradesQuery)
    {
        try
        {
            var connectionString = $"Data Source={_databasePath}";
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

            var result = new List<MinPEForTrade>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new MinPEForTrade
                {
                    TradeId = reader.GetInt64(reader.GetOrdinal("TradeId")),
                    AccountVolume = reader.GetDecimal(reader.GetOrdinal("WalletPerpEquity")),
                    MinPE = reader.GetDecimal(reader.GetOrdinal("MinPerpEquityForCopyTrade")),
                    Spread = reader.GetDecimal(reader.GetOrdinal("Spread")),
                    DeltaTimeS = reader.GetDecimal(reader.GetOrdinal("DeltaTimeS")),
                    SubType = reader.IsDBNull(reader.GetOrdinal("SubType"))
                        ? OrderSubType.None
                        : Enum.Parse<OrderSubType>(reader.GetString(reader.GetOrdinal("SubType"))),
                });
            }
            return [.. result];
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository MinPerpEquityForTradesQuery: {ex.Message}");
        }

        return [];
    }

    public async Task WriteMinPeForOrder(MinPEForOrder minPeForOrder)
    {
        try
        {
            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO MinPerpEquityForOrders
                    (OrderId, AccountVolume, MinPE, SubType)
                VALUES
                    (@OrderId, @AccountVolume, @MinPE, @SubType)
                ON CONFLICT(OrderId) DO UPDATE SET
                    SubType = excluded.SubType;
        ";

            command.Parameters.AddWithValue("@OrderId", minPeForOrder.OrderId);
            command.Parameters.AddWithValue("@AccountVolume", minPeForOrder.AccountVolume);
            command.Parameters.AddWithValue("@MinPE", minPeForOrder.MinPE);
            command.Parameters.AddWithValue("@SubType", minPeForOrder.SubType.ToString());

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Repository WriteMinPeForOrder: {ex.Message}");
        }
    }
}
