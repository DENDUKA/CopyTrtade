using CopyTrading.DataEvents;
using CopyTrading.Mappers;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CryptoExchange.Net.Objects;
using HyperLiquid.Net.Clients;

namespace CopyTrading.Providers.Hyperliquid.Subscribers;

public class OrdersTradesSubscriber(
    ILogger<OrdersTradesSubscriber> _logger,
    OrdersProvider _ordersProvider,
    IFillsOrderService _fillsOrderService,
    ICurrentWalletPositionService _currentWalletPositionService)
{
    private static readonly HashSet<Wallet> _orderSubscribes = [];
    private static readonly HashSet<Wallet> _pendingOrderSubscribes = [];

    private static readonly HashSet<Wallet> _tradeSubscribes = [];
    private static readonly HashSet<Wallet> _pendingTradeSubscribes = [];

    public (Wallet, bool)[] OrdersSubscriptionStatus => [.. _orderSubscribes.Select(x => (x, true)), .. _pendingOrderSubscribes.Select(x => (x, false))];
    public (Wallet, bool)[] TradesSubscriptionStatus => [.. _tradeSubscribes.Select(x => (x, true)), .. _pendingTradeSubscribes.Select(x => (x, false))];

    public void GetSubscribeStatus(Wallet wallet, out bool ordersSubscribed, out bool tradesSubscribed)
    {
        ordersSubscribed = _orderSubscribes.Contains(wallet);
        tradesSubscribed = _tradeSubscribes.Contains(wallet);
    }

    //TODO Переделать метод из рекурсии в while
    //Возможно стоит подумать о подписке параллельной
    //Подумать над onDisconnect Event
    public async Task SubscribeToNewOrders(Wallet[] wallets)
    {
        var walletsForSubscribe = new List<Wallet>();      

        foreach (var wallet in wallets)
        {
            if (_orderSubscribes.Contains(wallet) || _pendingOrderSubscribes.Contains(wallet))
                continue;

            _pendingOrderSubscribes.Add(wallet);
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
            (newOrders) =>
            {
                DataBusEvents.NewOrders.Invoke([.. newOrders.Data
                    .Where(x => x.Order.Quantity != 0)
                    .Select(x => x.ToBll(wallet))]);
            });

        _pendingOrderSubscribes.Remove(wallet);

        if (response.Success)
        {
            _orderSubscribes.Add(wallet);
            _logger.LogInformation($"SubscribeToNewOrders Успешно подписались на {wallet}");

            // Загружаем открытые ордера после успешной подписки
            await LoadActiveOrdersForWallet(wallet);

            return true;
        }
        else
        {
            _logger.LogWarning($"SubscribeToNewOrders Не удалось подписаться на {wallet} {response.Error}");
            return false;
        }
    }

    //TODO Переделать метод из рекурсии в while
    public async Task SubscribeToTrades(Wallet[] wallets)
    {
        var walletsForSubscribe = new List<Wallet>();

        foreach (var wallet in wallets)
        {
            if (_tradeSubscribes.Contains(wallet) || _pendingTradeSubscribes.Contains(wallet))
                continue;

            _pendingTradeSubscribes.Add(wallet);
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

        _pendingTradeSubscribes.Remove(wallet);

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

    /// <summary>
    /// Загружает все открытые (активные) ордера для указанного кошелька
    /// и добавляет их в FillsOrderService.
    /// Вызывается после успешной подписки на новые ордера.
    /// </summary>
    /// <param name="wallet">Кошелек для загрузки ордеров</param>
    private async Task LoadActiveOrdersForWallet(Wallet wallet)
    {
        try
        {
            _logger.LogInformation($"LoadActiveOrdersForWallet: Загрузка открытых ордеров для {wallet}");

            var activeOrders = await _ordersProvider.GetActiveOrders(wallet);

            if (activeOrders.Length == 0)
            {
                _logger.LogInformation($"LoadActiveOrdersForWallet: У {wallet} нет открытых ордеров");
                return;
            }

            int addedCount = _fillsOrderService.AddHistoricalOrders(activeOrders);

            _logger.LogInformation(
                $"LoadActiveOrdersForWallet: Для {wallet} загружено {activeOrders.Length} открытых ордеров, " +
                $"добавлено {addedCount} новых");

            // Пересчитываем SubType для всех загруженных ордеров
            var uniqueSymbols = activeOrders.Select(o => o.Symbol).Distinct().ToArray();
            foreach (var symbol in uniqueSymbols)
            {
                _currentWalletPositionService.RecalculateSubTypesForSymbol(wallet, symbol);
            }

            _logger.LogInformation(
                $"LoadActiveOrdersForWallet: Пересчитан SubType для {uniqueSymbols.Length} символов");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"LoadActiveOrdersForWallet: Ошибка при загрузке открытых ордеров для {wallet}");
        }
    }
}