
using CopyTrading.Models;

public record WalletInfoModel
{
    public string Wallet { get; set; }
    public double AccountVolume { get; set; }
    public decimal TotalMarginUsed { get; set; }
    public Dictionary<string, PositionModel> Positions { get; set; }

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