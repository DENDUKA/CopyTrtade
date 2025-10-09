using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopyTrading.Models.Models.Enums.HyperLiquid;

public enum OrderStatus
{
    Unknown,
    Open,
    Filled,
    Canceled,
    Triggered,
    Rejected,
    MarginCanceled,
    VaultWithdrawalCanceled,
    OpenInterestCapCanceled,
    SelfTradeCanceled,
    ReduceOnlyCanceled,
    SiblingFilledCanceled,
    DelistedCanceled,
    LiquidatedCanceled,
    ScheduledCancel,
    TickRejected,
    MinTradeNtlRejected,
    PerpMarginRejected,
    ReduceOnlyRejected,
    BadAloPxRejected,
    IocCancelRejected,
    BadTriggerPxRejected,
    MarketOrderNoLiquidityRejected,
    PositionIncreaseAtOpenInterestCapRejected,
    PositionFlipAtOpenInterestCapRejected,
    TooAggressiveAtOpenInterestCapRejected,
    OpenInterestIncreaseRejected,
    InsufficientSpotBalanceRejected,
    OracleRejected,
    PerpMaxPositionRejected
}

// Кастомный конвертер для десериализации статусов из snake_case
public class OrderStatusJsonConverter : JsonConverter<OrderStatus>
{
    private static readonly Dictionary<string, OrderStatus> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = OrderStatus.Open,
        ["filled"] = OrderStatus.Filled,
        ["canceled"] = OrderStatus.Canceled,
        ["triggered"] = OrderStatus.Triggered,
        ["rejected"] = OrderStatus.Rejected,
        ["marginCanceled"] = OrderStatus.MarginCanceled,
        ["vaultWithdrawalCanceled"] = OrderStatus.VaultWithdrawalCanceled,
        ["openInterestCapCanceled"] = OrderStatus.OpenInterestCapCanceled,
        ["selfTradeCanceled"] = OrderStatus.SelfTradeCanceled,
        ["reduceOnlyCanceled"] = OrderStatus.ReduceOnlyCanceled,
        ["siblingFilledCanceled"] = OrderStatus.SiblingFilledCanceled,
        ["delistedCanceled"] = OrderStatus.DelistedCanceled,
        ["liquidatedCanceled"] = OrderStatus.LiquidatedCanceled,
        ["scheduledCancel"] = OrderStatus.ScheduledCancel,
        ["tickRejected"] = OrderStatus.TickRejected,
        ["minTradeNtlRejected"] = OrderStatus.MinTradeNtlRejected,
        ["perpMarginRejected"] = OrderStatus.PerpMarginRejected,
        ["reduceOnlyRejected"] = OrderStatus.ReduceOnlyRejected,
        ["badAloPxRejected"] = OrderStatus.BadAloPxRejected,
        ["iocCancelRejected"] = OrderStatus.IocCancelRejected,
        ["badTriggerPxRejected"] = OrderStatus.BadTriggerPxRejected,
        ["marketOrderNoLiquidityRejected"] = OrderStatus.MarketOrderNoLiquidityRejected,
        ["positionIncreaseAtOpenInterestCapRejected"] = OrderStatus.PositionIncreaseAtOpenInterestCapRejected,
        ["positionFlipAtOpenInterestCapRejected"] = OrderStatus.PositionFlipAtOpenInterestCapRejected,
        ["tooAggressiveAtOpenInterestCapRejected"] = OrderStatus.TooAggressiveAtOpenInterestCapRejected,
        ["openInterestIncreaseRejected"] = OrderStatus.OpenInterestIncreaseRejected,
        ["insufficientSpotBalanceRejected"] = OrderStatus.InsufficientSpotBalanceRejected,
        ["oracleRejected"] = OrderStatus.OracleRejected,
        ["perpMaxPositionRejected"] = OrderStatus.PerpMaxPositionRejected
    };

    public override OrderStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var key = reader.GetString();
        if (key != null && _map.TryGetValue(key, out var status))
            return status;
        throw new JsonException($"Unknown order status: {key}");
    }

    public override void Write(Utf8JsonWriter writer, OrderStatus value, JsonSerializerOptions options)
    {
        var key = _map.FirstOrDefault(x => x.Value == value).Key ?? value.ToString();
        writer.WriteStringValue(key);
    }
}
