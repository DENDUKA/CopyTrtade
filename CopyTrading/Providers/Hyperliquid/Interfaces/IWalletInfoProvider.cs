using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;

namespace CopyTrading.Providers.Hyperliquid.Interfaces;

public interface IWalletInfoProvider
{
    Task<OriginalOrder[]> GetHistoricalOrders(Wallet wallet);
    Task<OriginalTrade[]> GetHistoricalTrades(Wallet wallet);
    Task<WalletInfoModel?> GetInfo(Wallet wallet, bool useCache = true);
    Task<string> QueryPortfolio(Wallet wallet);
}
