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

    /// <summary>
    /// Могут быть расхождения по времени , к примеру трейда и получению OrderBook, для этого вводится dateTime
    /// Если время получения OrderBook > dateTime то возвращаем ответ
    /// Иначе ждем пока получим актуальный OrderBook
    /// </summary>
    /// <param name="coin"></param>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public async Task<HyperLiquidOrderBook> GetOrderBook(string coin, DateTime dateTime)
    {
        if (!_orderBook.ContainsKey(coin))
        {
            await SubscribeToOrderBook(coin);
        }

        do
        {
            if (_orderBook.TryGetValue(coin, out HyperLiquidOrderBook? orderBook) && orderBook is not null && orderBook.Timestamp > dateTime)
            {
                return orderBook;
            }

            await Task.Delay(10);
        } while (true);
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