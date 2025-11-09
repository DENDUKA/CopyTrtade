using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models;

public record CopyTradeWalletSettings
{
    public Wallet Wallet { get; init; } = null!;
    public decimal VolumeUsd { get; set; }
    public decimal CopyKoef { get; set; } = 1.0m;

    public override string ToString()
        => $"Wallet: {Wallet}, VolumeUsd: {VolumeUsd}, CopyKoef: {CopyKoef}";
}
