using CopyTrading.Models;
using CopyTrading.Values;
using Newtonsoft.Json;

namespace CopyTrading.Repository.SQLite.Dto;

public class WalletSnapshotPositionsDto
{
    public WalletSnapshotPositionsDto(WalletPositionsSnapshot walletSnapshot)
    {
        Wallet = walletSnapshot.Wallet;
        TimeStamp = walletSnapshot.TimeStamp;
        Positions = JsonConvert.SerializeObject(walletSnapshot.Positions);
    }

    public Wallet Wallet { get; set; }
    public DateTime TimeStamp { get; set; }
    public string Positions { get; set; }
}