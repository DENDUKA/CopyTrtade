using CopyTrading.Models.Models;
using CopyTrading.Models.Values;

namespace CopyTrading.Repository.SQLite;

public interface IWalletSettingsRepository
{
    Task Upsert(CopyTradeWalletSettings settings);
    Task<CopyTradeWalletSettings?> Get(Wallet wallet);
    Task<CopyTradeWalletSettings[]> GetAll();
    Task<bool> Delete(Wallet wallet);
}
