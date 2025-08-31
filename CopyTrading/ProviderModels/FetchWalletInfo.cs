using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace CopyTrading.ProviderModels;

public partial class WalletInfoProviderModel
{
    [JsonProperty("accountValueHistory")]
    public History[][] AccountValueHistory { get; set; }

    [JsonProperty("pnlHistory")]
    public History[][] PnlHistory { get; set; }

    [JsonProperty("vlm")]
    public string Vlm { get; set; }
}

public partial struct History
{
    public long? Integer;
    public string String;

    public static implicit operator History(long Integer) => new History { Integer = Integer };
    public static implicit operator History(string String) => new History { String = String };
}

public partial struct WalletInfoElement
{
    public string String;
    public WalletInfoProviderModel WalletInfoClass;

    public static implicit operator WalletInfoElement(string String) => new WalletInfoElement { String = String };
    public static implicit operator WalletInfoElement(WalletInfoProviderModel Welcome7Class) => new WalletInfoElement { WalletInfoClass = Welcome7Class };
}

public class WalletInfo
{
    public static WalletInfoElement[][] FromJson(string json) => JsonConvert.DeserializeObject<WalletInfoElement[][]>(json, Converter.Settings);
}

public static class Serialize
{
    public static string ToJson(this WalletInfoElement[][] self) => JsonConvert.SerializeObject(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
        Converters =
        {
            WalletInfoElementConverter.Singleton,
            HistoryConverter.Singleton,
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
    };
}

internal class WalletInfoElementConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(WalletInfoElement) || t == typeof(WalletInfoElement?);

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.String:
            case JsonToken.Date:
                var stringValue = serializer.Deserialize<string>(reader);
                return new WalletInfoElement { String = stringValue };
            case JsonToken.StartObject:
                var objectValue = serializer.Deserialize<WalletInfoProviderModel>(reader);
                return new WalletInfoElement { WalletInfoClass = objectValue };
        }
        throw new Exception("Cannot unmarshal type WalletInfoElement");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        var value = (WalletInfoElement)untypedValue;
        if (value.String != null)
        {
            serializer.Serialize(writer, value.String);
            return;
        }
        if (value.WalletInfoClass != null)
        {
            serializer.Serialize(writer, value.WalletInfoClass);
            return;
        }
        throw new Exception("Cannot marshal type WalletInfoElement");
    }

    public static readonly WalletInfoElementConverter Singleton = new WalletInfoElementConverter();
}

internal class HistoryConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(History) || t == typeof(History?);

    public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Integer:
                var integerValue = serializer.Deserialize<long>(reader);
                return new History { Integer = integerValue };
            case JsonToken.String:
            case JsonToken.Date:
                var stringValue = serializer.Deserialize<string>(reader);
                return new History { String = stringValue };
        }
        throw new Exception("Cannot unmarshal type History");
    }

    public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
    {
        var value = (History)untypedValue;
        if (value.Integer != null)
        {
            serializer.Serialize(writer, value.Integer.Value);
            return;
        }
        if (value.String != null)
        {
            serializer.Serialize(writer, value.String);
            return;
        }
        throw new Exception("Cannot marshal type History");
    }

    public static readonly HistoryConverter Singleton = new HistoryConverter();
}
