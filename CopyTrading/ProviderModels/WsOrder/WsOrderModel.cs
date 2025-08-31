using CopyTrading.Models.Enums;
using System.Text.Json.Serialization;

namespace CopyTrading.ProviderModels.WsOrder;

public class WsOrderModel
{
    [JsonPropertyName("order")]
    public WsBasicOrder Order { get; set; } = default!;

    [JsonPropertyName("status")]
    [JsonConverter(typeof(OrderStatusJsonConverter))]
    public OrderStatus Status { get; set; }
    
    [JsonPropertyName("statusTimestamp")]
    public long StatusTimestamp { get; set; }
}

public class WsBasicOrder
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("limitPx")]
    public string LimitPx { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Sz { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("origSz")]
    public string OrigSz { get; set; } = string.Empty;

    [JsonPropertyName("cloid")]
    public string? Cloid { get; set; }
}