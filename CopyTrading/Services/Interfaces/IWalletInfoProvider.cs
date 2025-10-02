using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using CopyTrading.Values;

namespace CopyTrading.Services.Interfaces;

public interface IWalletInfoProvider
{
    Task<OriginalOrder[]> GetHistoricalOrders(Wallet wallet);
    Task<OriginalTrade[]> GetHistoricalTrades(Wallet wallet);
    Task<WalletInfoModel?> GetInfo(Wallet wallet, bool useCache = true);
    Task<string> QueryPortfolio(Wallet wallet);
}
