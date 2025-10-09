using CopyTrading.Models.Values;

namespace CopyTrading.Models;

public record WalletInfoModel
{
    public Wallet Wallet { get; set; }
    public decimal AccountValue { get; set; }
    public decimal TotalMarginUsed { get; set; }
    public Dictionary<string, Position> Positions { get; set; }

    public override string ToString()
    {
        var positionsString = Positions == null || Positions.Count == 0
            ? "Нет позиций"
            : string.Join("\n", Positions.Select(p => $"{p.Value}"));

        return
            $"Wallet: {Wallet}, " +
            $"AccountValue: {AccountValue}, " +
            $"TotalMarginUsed: {TotalMarginUsed}, " +
            $"\nPositions: \n{positionsString}";
    }
}