using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ICopyOrderStorageService
{
    Task<bool> AddOrder(CopyOrderV2 order);
    Task<CopyOrderV2[]> GetAllOrders();
    Task<CopyOrderV2[]> GetOrdersByStatus(OrderStatus status);
    Task<CopyOrderV2[]> GetOrdersByOriginalOrderId(long originalOrderId);
    Task<CopyOrderV2[]> GetOrdersByWalletAndSymbol(Wallet wallet, string symbol);
    Task ClearAllOrders();
    Task<Dictionary<string, int>> GetStatistics();
}
