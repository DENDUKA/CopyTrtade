using CopyTrading.Models.Enums;
using CopyTrading.Models.Trade;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Settings;

namespace CopyTrading.Services;

public class TradeService(
    OrdersTradesSubscriber _orderProvider,
    OrderBookSubscriber _orderBookProvider,
    WalletInfoProvider _walletInfoProvider,
    TradeRepository _tradeRepository)
{
    public async Task SubscribeToWalletTrades(string wallet)
    {
        await _orderProvider.SubscribeToFilledTrades(wallet, NewTrades);
    }

    public async Task SubscribeToTrackedWalletsTrades()
    {
        foreach (var wallet in WalletSettings.TestFillTrackedWallets)
        {
            await _orderProvider.SubscribeToFilledTrades(wallet, NewTrades);
        }
    }

    public async Task CollectHystoricalTrades(string wallet)
    {
        var trades = await _walletInfoProvider.GetHistoricalTrades(wallet);

        _tradeRepository.WriteTrades(trades);
    }

    private async void NewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        if (newTrades.IsSnapshot)
        {
            Console.WriteLine("___SnapShot___");
        }

        foreach (var trade in newTrades.Trades)
        {
            Console.WriteLine($"Trade {trade.Coin}");

            if (!newTrades.IsSnapshot)
            {
                await IsTradePriceActual(trade);
            }
        }
    }

    private async Task IsTradePriceActual(OriginalTrade trade)
    {
        var spreadDelta = 0.01;

        var orderBook = await _orderBookProvider.GetOrderBook(trade.Coin);

        if (orderBook is null) return;

        var bestAsk = (double)orderBook.Levels.Asks.First().Price;
        var bestBid = (double)orderBook.Levels.Bids.First().Price;

        Console.WriteLine($"Trade Time : {trade.TimeStamp}\n" +
                          $"OB    Time : {orderBook.Timestamp}");

        if (trade.Direction == Direction.Long)
        {
            var spread = (bestAsk - trade.Price) / bestAsk * 100;

            if (trade.Price >= bestAsk || spread < spreadDelta)
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} актуальна для покупки. {spread:F5} %");
            }
            else
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestAsk} не актуальна для покупки. {spread:F5} %");
            }
        }
        else
        {
            var spread = (bestBid - trade.Price) / bestBid * 100;

            if (trade.Price <= bestBid || spread < spreadDelta)
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} актуальна для продажи. {spread:F5} %");
            }
            else
            {
                Console.WriteLine($"Цена сделки {trade.TradeId} {trade.Direction} {trade.Price} сейчас {bestBid} не актуальна для продажи. {spread:F5} %");
            }
        }
    }
}
