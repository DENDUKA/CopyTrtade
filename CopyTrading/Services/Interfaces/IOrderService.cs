using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface IOrderService
{
    Task SubscribeToWalletOrders(Wallet wallet);
    Task SubscribeToTrackedWalletsOrders();
    Task CollectHistoryOrders();
    Task CloseOrderWithStatus(long orderId, OrderStatus status);
}
