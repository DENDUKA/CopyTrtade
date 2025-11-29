using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

public interface ITradeService
{
    Task SubscribeToWalletTrades(Wallet wallet);
    Task SubscribeToTrackedWalletsTrades();
}
