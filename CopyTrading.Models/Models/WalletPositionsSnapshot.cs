using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models;

public record WalletPositionsSnapshot
{
    public Wallet Wallet { get; set; }
    public DateTime TimeStamp { get; set; }
    public List<Position> Positions { get; set; }
}
