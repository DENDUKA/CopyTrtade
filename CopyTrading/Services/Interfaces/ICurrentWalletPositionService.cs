using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICurrentWalletPositionService
{
    Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades);
    void InitializeWalletSnapshot(WalletPositionsSnapshot snapshot);
    void ClearAllSnapshots();
    Task<OrderSubType> AddTrade(OriginalTrade trade);
    Task<WalletPositionsSnapshot> GetSnapshot(Wallet wallet);
    OrderSubType GetOrderSubType(OriginalOrder order);
    Task<int?> TryGetLeverage(Wallet wallet, string symbol);
    void RecalculateSubTypesForSymbol(Wallet wallet, string symbol);
    IEnumerable<Wallet> GetAllWallets();
}
