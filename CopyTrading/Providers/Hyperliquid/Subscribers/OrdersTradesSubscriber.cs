using CopyTrading.Mappers;
using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using CopyTrading.Values;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Clients;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrdersTradesSubscriber
{
    private static readonly HashSet<Wallet> _orderSubscribes = [];
    private static readonly HashSet<Wallet> _tradeSubscribes = [];

    public async Task SubscribeToNewOrders(Wallet wallet, Action<OriginalOrder[]> newOrderAction)
    {
        if (_orderSubscribes.Contains(wallet))
            return;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToOrderUpdatesAsync(wallet.Value,
            (newOrders) => newOrderAction.Invoke(newOrders.Data.Select(x => x.ToBll(wallet)).ToArray()));

        if (response.Success)
        {
            _orderSubscribes.Add(wallet);
            Console.WriteLine($"SubscribeToNewOrders Успешно подписались на {wallet}");
        }
        else
        {
            Console.WriteLine($"SubscribeToNewOrders Не удалось подписаться на {wallet}");
        }
    }

    public async Task SubscribeToFilledTrades(Wallet wallet, Action<(OriginalTrade[] Trades, bool IsSnapshot)> newTradeAction)
    {
        if (_tradeSubscribes.Contains(wallet))
            return;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToUserTradeUpdatesAsync(wallet.Value,
            (newTrades) => newTradeAction.Invoke(
                (newTrades.Data.Select(
                    x => x.ToBll(wallet)).ToArray(),
                    newTrades.UpdateType == SocketUpdateType.Snapshot)));

        if (response.Success)
        {
            _tradeSubscribes.Add(wallet);
            Console.WriteLine($"Успешно подписались на Trades {wallet}");
        }
        else
        {
            Console.WriteLine($"Не удалось подписаться на Trades {wallet}");
            _tradeSubscribes.Remove(wallet);
        }
    }
}