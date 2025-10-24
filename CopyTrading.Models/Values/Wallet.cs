using System.Text.Json.Serialization;

namespace CopyTrading.Models.Values;

public record Wallet
{
    public string Value { get; }

    [JsonConstructor]
    public Wallet(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("Некорректный формат кошелька.", nameof(value));
        }

        Value = value;
    }

    public static bool IsValid(string wallet)
    {
        if (string.IsNullOrWhiteSpace(wallet))
            return false;
        if (wallet.Length != 42)
            return false;
        if (!wallet.StartsWith("0x"))
            return false;
        for (int i = 2; i < wallet.Length; i++)
        {
            if (!Uri.IsHexDigit(wallet[i]))
                return false;
        }
        return true;
    }

    public override string ToString()
    {
        return Value;
    }
}
