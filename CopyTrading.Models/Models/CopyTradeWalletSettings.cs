using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models;

public record CopyTradeWalletSettings
{
    public Wallet Wallet { get; init; } = null!;
    public decimal VolumeUsd { get; set; }

    public override string ToString()
        => $"Wallet: {Wallet}, VolumeUsd: {VolumeUsd}";
}
