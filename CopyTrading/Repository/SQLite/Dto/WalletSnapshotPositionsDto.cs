using CopyTrading.Models.Models;
using CopyTrading.Models.Values;
using Newtonsoft.Json;

namespace CopyTrading.Repository.SQLite.Dto;

public class WalletSnapshotPositionsDto(WalletPositionsSnapshot walletSnapshot)
{
    public Wallet Wallet { get; set; } = walletSnapshot.Wallet;
    public DateTime TimeStamp { get; set; } = walletSnapshot.TimeStamp;
    public string Positions { get; set; } = JsonConvert.SerializeObject(walletSnapshot.Positions);
}