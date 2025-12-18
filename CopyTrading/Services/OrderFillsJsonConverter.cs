using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;

namespace CopyTrading.Services;

/// <summary>
/// Кастомный JSON конвертер для OrderFills для сериализации/десериализации в Redis
/// </summary>
public class OrderFillsJsonConverter : JsonConverter<OrderFills>
{
    public override OrderFills? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        OriginalOrder? originalOrder = null;
        List<OriginalTrade>? trades = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            string? propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "OriginalOrder":
                    originalOrder = JsonSerializer.Deserialize<OriginalOrder>(ref reader, options);
                    break;
                case "Trades":
                    trades = JsonSerializer.Deserialize<List<OriginalTrade>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (originalOrder == null)
        {
            throw new JsonException("OriginalOrder is required");
        }

        var orderFills = new OrderFills(originalOrder);

        // Добавляем trades в ConcurrentBag
        if (trades != null)
        {
            foreach (var trade in trades)
            {
                orderFills.Trades.Add(trade);
            }
        }

        return orderFills;
    }

    public override void Write(Utf8JsonWriter writer, OrderFills value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Сериализуем OriginalOrder
        writer.WritePropertyName("OriginalOrder");
        JsonSerializer.Serialize(writer, value.OriginalOrder, options);

        // Сериализуем Trades как массив
        writer.WritePropertyName("Trades");
        writer.WriteStartArray();
        foreach (var trade in value.Trades)
        {
            JsonSerializer.Serialize(writer, trade, options);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
