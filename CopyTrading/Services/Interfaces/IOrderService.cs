using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface IOrderService
{
    Task SubscribeToWalletOrders(Wallet wallet);
    Task SubscribeToTrackedWalletsOrders();
    Task CollectHistoryOrders();
}
