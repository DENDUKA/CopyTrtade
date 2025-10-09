namespace CopyTrading.Models.Values;

public record Wallet
{
    public string Value { get; }

    public Wallet(string wallet)
    {
        if (!IsValid(wallet))
        {
            throw new ArgumentException("Некорректный формат кошелька.", nameof(wallet));
        }

        Value = wallet;
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
