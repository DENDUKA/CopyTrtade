using CopyTrading.Repository.SQLite.Dto;

namespace CopyTrading.Repository.SQLInterfaces.Interfaces;

public interface IWalletInfoRepository
{
    Task WriteCurrentPositions(WalletSnapshotPositionsDto dto);
}
