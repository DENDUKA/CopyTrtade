using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICopyOrderStorageService
{
    bool AddOrder(CopyOrderV2 order);
    CopyOrderV2[] GetAllOrders();
    CopyOrderV2[] GetOrdersByStatus(OrderStatus status);
    CopyOrderV2[] GetOrdersByOriginalOrderId(long originalOrderId);
    CopyOrderV2[] GetOrdersByWalletAndSymbol(Wallet wallet, string symbol);
    void ClearAllOrders();
    Dictionary<string, int> GetStatistics();
}
