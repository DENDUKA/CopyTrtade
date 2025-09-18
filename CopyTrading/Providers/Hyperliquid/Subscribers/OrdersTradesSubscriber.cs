using CopyTrading.Mappers;
using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Clients;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrdersTradesSubscriber
{
    private static HashSet<string> subscribes = [];

    public async Task SubscribeToNewOrders(string wallet, Action<OriginalOrder[]> newOrderAction)
    {
        if (subscribes.Contains(wallet))
            return;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToOrderUpdatesAsync(wallet,
            (newOrders) => newOrderAction.Invoke(newOrders.Data.Select(x => x.ToBll(wallet)).ToArray()));

        if (response.Success)
        {
            subscribes.Add(wallet);
            Console.WriteLine($"Успешно подписались на {wallet}");
        }
        else
        {
            Console.WriteLine($"Не удалось подписаться на {wallet}");
        }
    }

    public async Task SubscribeToFilledTrades(string wallet, Action<(OriginalTrade[] Trades, bool IsSnapshot)> newTradeAction)
    {
        if (subscribes.Contains(wallet))
            return;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToUserTradeUpdatesAsync(wallet, (newTrades) =>
            newTradeAction.Invoke((newTrades.Data.Select(x => x.ToBll(wallet)).ToArray(), newTrades.UpdateType == SocketUpdateType.Snapshot)));

        if (response.Success)
        {
            subscribes.Add(wallet);
            Console.WriteLine($"Успешно подписались на Trades {wallet}");
        }
        else
        {
            Console.WriteLine($"Не удалось подписаться на Trades {wallet}");
            subscribes.Remove(wallet);
        }
    }
}
