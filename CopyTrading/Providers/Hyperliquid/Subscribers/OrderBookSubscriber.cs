using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrderBookSubscriber(ILogger<OrderBookSubscriber> _logger)
{
    private readonly Dictionary<string, HyperLiquidOrderBook> _orderBook = [];
    private readonly object _lock = new();

    public async Task<HyperLiquidOrderBook> GetOrderBook(string symbol)
    {
        if (!_orderBook.ContainsKey(symbol))
        {
            await SubscribeToOrderBook(symbol);
        }

        return _orderBook[symbol];
    }

    /// <summary>
    /// Могут быть расхождения по времени , к примеру трейда и получению OrderBook, для этого вводится dateTime
    /// Если время получения OrderBook > dateTime то возвращаем ответ
    /// Иначе ждем пока получим актуальный OrderBook
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public async Task<HyperLiquidOrderBook> GetOrderBook(string symbol, DateTime dateTime)
    {
        if (!_orderBook.ContainsKey(symbol))
        {
            await SubscribeToOrderBook(symbol);
        }

        do
        {
            if (_orderBook.TryGetValue(
                symbol, 
                out HyperLiquidOrderBook? orderBook) && 
                orderBook is not null && 
                orderBook.Levels.Asks.Length > 0 && 
                orderBook.Levels.Bids.Length > 0 && 
                orderBook.Timestamp > dateTime)
            {
                return orderBook;
            }

            await Task.Delay(10);
        } while (true);
    }

    public async Task SubscribeToOrderBook(string symbol)
    {
        HyperLiquidSocketClient _socketClient = new();

        if (!_orderBook.ContainsKey(symbol))
        {
            Task<CallResult<UpdateSubscription>> task = null;
            lock (_lock)
            {
                if (!_orderBook.ContainsKey(symbol))
                {
                    _orderBook.Add(symbol, null);

                    task = _socketClient.FuturesApi.SubscribeToOrderBookUpdatesAsync(symbol,
                        (orderBook) => OrderBookUpdate(orderBook));
                }
            }

            if (task is not null)
            {
                var response = await task;
                if (response.Success)
                {
                    _logger.LogInformation($"Успешно подписались на OrderBook {symbol}");
                }
                else
                {
                    _logger.LogWarning($"Не смогли подписаться на OrderBook {symbol}");
                    _orderBook.Remove(symbol);
                }
            }
        }
    }

    private async Task OrderBookUpdate(DataEvent<HyperLiquidOrderBook> orderBook)
    {
        _orderBook[orderBook.Data.Symbol] = orderBook.Data;
    }
}