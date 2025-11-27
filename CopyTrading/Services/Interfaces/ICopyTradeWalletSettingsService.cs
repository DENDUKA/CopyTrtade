using CopyTrading.Models.Models;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICopyTradeWalletSettingsService
{
    Task<CopyTradeWalletSettings> CreateOrUpdate(CopyTradeWalletSettings settings);
    Task<CopyTradeWalletSettings?> Get(Wallet wallet);
    Task<IEnumerable<CopyTradeWalletSettings>> GetAll();
    Task<bool> Delete(CopyTradeWalletSettings settings);
}
