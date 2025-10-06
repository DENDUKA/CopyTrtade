using CopyTrading.DataEvents;
using CopyTrading.Mappers;
using CopyTrading.Values;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Clients;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrdersTradesSubscriber(ILogger<OrdersTradesSubscriber> _logger)
{
    private static readonly HashSet<Wallet> _orderSubscribes = [];
    private static readonly HashSet<string> _pendingOrderSubscribes = [];

    private static readonly HashSet<Wallet> _tradeSubscribes = [];
    private static readonly HashSet<string> _pendingTradeSubscribes = [];

    public async Task SubscribeToNewOrders(Wallet[] wallets)
    {
        var walletsForSubscribe = new List<Wallet>();      

        foreach (var wallet in wallets)
        {
            if (_orderSubscribes.Contains(wallet) || _pendingOrderSubscribes.Contains(wallet.Value))
                continue;

            _pendingOrderSubscribes.Add(wallet.Value);
            walletsForSubscribe.Add(wallet);
        }

        var successCount = 0;

        foreach (var wallet in walletsForSubscribe)
        {
            if (await SubscribeToNewOrders(wallet)) successCount++;
        }

        _logger.LogInformation($"SubscribeToNewOrders Успешно/НеУспешно {successCount}/{walletsForSubscribe.Count - successCount}");

        var unSubscribes = walletsForSubscribe.Where(x => !_orderSubscribes.Contains(x)).ToArray();

        if (unSubscribes.Length != 0)
        {
            await SubscribeToNewOrders(unSubscribes);
        }
    }

    private async Task<bool> SubscribeToNewOrders(Wallet wallet)
    {
        if (_orderSubscribes.Contains(wallet)) return true;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToOrderUpdatesAsync(wallet.Value,
            (newOrders) => DataBusEvents.NewOrders.Invoke(newOrders.Data.Select(x => x.ToBll(wallet)).ToArray()));

        _pendingOrderSubscribes.Remove(wallet.Value);

        if (response.Success)
        {
            _orderSubscribes.Add(wallet);
            _logger.LogInformation($"SubscribeToNewOrders Успешно подписались на {wallet}");            
            return true;
        }
        else
        {
            _logger.LogWarning($"SubscribeToNewOrders Не удалось подписаться на {wallet} {response.Error}");
            return false;
        }
    }

    public async Task SubscribeToTrades(Wallet[] wallets)
    {
        var walletsForSubscribe = new List<Wallet>();

        foreach (var wallet in wallets)
        {
            if (_tradeSubscribes.Contains(wallet) || _pendingTradeSubscribes.Contains(wallet.Value))
                continue;

            _pendingTradeSubscribes.Add(wallet.Value);
            walletsForSubscribe.Add(wallet);
        }

        var successCount = 0;

        foreach (var wallet in walletsForSubscribe)
        {
            if (await SubscribeToTrades(wallet)) successCount++;
        }

        _logger.LogInformation($"SubscribeToFilledTrades Успешно/НеУспешно {successCount}/{walletsForSubscribe.Count - successCount}");

        var unSubscribes = walletsForSubscribe.Where(x => !_tradeSubscribes.Contains(x)).ToArray();

        if (unSubscribes.Length != 0)
        {
            await SubscribeToTrades(unSubscribes);
        }
    }

    private async Task<bool> SubscribeToTrades(Wallet wallet)
    {
        if (_tradeSubscribes.Contains(wallet)) return true;

        HyperLiquidSocketClient _socketClient = new();

        var response = await _socketClient.FuturesApi.SubscribeToUserTradeUpdatesAsync(wallet.Value,
            (newTrades) => DataBusEvents.NewTrades.Invoke(
                (newTrades.Data.Select(
                    x => x.ToBll(wallet)).ToArray(),
                    newTrades.UpdateType == SocketUpdateType.Snapshot)));

        _pendingTradeSubscribes.Remove(wallet.Value);

        if (response.Success)
        {
            _tradeSubscribes.Add(wallet);
            _logger.LogInformation($"Успешно подписались на Trades {wallet}");            
            return true;
        }
        else
        {
            _logger.LogWarning($"Не удалось подписаться на Trades {wallet} {response.Error}");
            return false;
        }
    }
}