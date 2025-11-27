using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface IFillsOrderService
{
    OrderFills[] GetAllOrderFills();
    OrderFills? GetOrderFillsByOrderId(long orderId);
    OrderFills[] GetOpenOrdersByWallet(Wallet wallet);
    OrderFills[] GetPendingOrdersByWalletAndSymbol(Wallet wallet, string symbol);
    bool UpdateOrderSubType(long orderId, OrderSubType newSubType);
    int AddHistoricalOrders(OriginalOrder[] orders);
    void OnNewOrders(OriginalOrder[] orders);
    void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades);
}
