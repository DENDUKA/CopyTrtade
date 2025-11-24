using CopyTrading.Repository.SQLite.Dto;

namespace CopyTrading.Repository.SQLite;

public interface IWalletInfoRepository
{
    Task WriteCurrentPositions(WalletSnapshotPositionsDto dto);
}
