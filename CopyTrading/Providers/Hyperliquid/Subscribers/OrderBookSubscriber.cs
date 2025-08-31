using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrderBookSubscriber
{
    private readonly Dictionary<string, HyperLiquidOrderBook> _orderBook = [];
    private readonly object _lock = new();
    public async Task<HyperLiquidOrderBook> GetOrderBook(string coin)
    {
        if (!_orderBook.ContainsKey(coin))
        {
            await SubscribeToOrderBook(coin);
        }

        return _orderBook[coin];
    }

    public async Task SubscribeToOrderBook(string coin)
    {
        HyperLiquidSocketClient _socketClient = new();

        if (!_orderBook.ContainsKey(coin))
        {
            Task<CallResult<UpdateSubscription>> task = null;
            lock (_lock)
            {
                if (!_orderBook.ContainsKey(coin))
                {
                    _orderBook.Add(coin, null);

                    task = _socketClient.FuturesApi.SubscribeToOrderBookUpdatesAsync(coin,
                        (orderBook) => OrderBookUpdate(orderBook));
                }
            }

            if (task is not null)
            {
                var response = await task;
                if (response.Success)
                {
                    Console.WriteLine($"Успешно подписались на OrderBook {coin}");
                }
                else
                {
                    Console.WriteLine($"!!! Не успешно подписались на OrderBook {coin}");
                    _orderBook.Remove(coin);
                }
            }
        }
    }

    private async Task OrderBookUpdate(DataEvent<HyperLiquidOrderBook> orderBook)
    {
        _orderBook[orderBook.Data.Symbol] = orderBook.Data;
    }
}