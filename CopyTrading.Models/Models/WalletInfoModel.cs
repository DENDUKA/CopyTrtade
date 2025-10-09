using CopyTrading.Models.Values;
using CopyTrading.Models.Models;

public record WalletInfoModel
{
    public Wallet Wallet { get; set; }
    public decimal AccountVolume { get; set; }
    public decimal TotalMarginUsed { get; set; }
    public Dictionary<string, Position> Positions { get; set; }
    public DateTime TimeStamp { get; set; }

    public override string ToString()
    {
        var positionsString = Positions == null || Positions.Count == 0
            ? "Нет позиций"
            : string.Join("\n", Positions.Select(p => $"{p.Value}"));

        return
            $"Wallet: {Wallet}, " +
            $"AccountValue: {AccountVolume}, " +
            $"TotalMarginUsed: {TotalMarginUsed}, " +
            $"\nPositions: \n{positionsString}";
    }
}