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
    Task<OrderSubType> GetOrderSubType(OriginalOrder order);
    Task<int?> TryGetLeverage(Wallet wallet, string symbol);
    Task RecalculateSubTypesForSymbol(Wallet wallet, string symbol);
    IEnumerable<Wallet> GetAllWallets();
    Task<decimal> CalculatePotentialPosition(OriginalOrder order);
    Task<decimal> CalculatePendingOrdersQuantity(OriginalOrder order);
}
