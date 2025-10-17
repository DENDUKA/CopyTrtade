using CopyTrading.Models.Values;
using HyperLiquid.Net.Enums;

namespace CopyTrading.Models.Models.Orders;

public class NewLeverageModel
{
    public string Symbol { get; init; }
    public int Leverage { get; init; }
    public MarginType MarginType { get; init; }
    public Wallet? VaultWallet { get; init; }
}
