using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using SQLLiteOrderRepository = CopyTrading.Repository.SQLite.OrderRepository;
using TradeRepositorySQL = CopyTrading.Repository.SQLite.TradeRepository;

namespace CopyTrading.Services;

public class OrderService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfo;
    private readonly OrderRepository _orderDBProvider;
    private readonly IExchangeInfoProvider _exchangeInfoProvider;
    private readonly SQLLiteOrderRepository _orderSQLLiteRepository;
    private readonly TradeRepositorySQL _tradeRepositorySQL;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly FillsOrderService _fillsOrderService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfo,
        OrderRepository orderDBProvider,
        IExchangeInfoProvider exchangeInfoProvider,
        SQLLiteOrderRepository orderSQLLiteProvider,
        TradeRepositorySQL tradeRepositorySQL,
        CurrentWalletPositionService currentWalletPositionService,
        FillsOrderService fillsOrderService,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _orderSQLLiteRepository = orderSQLLiteProvider;
        _currentWalletPositionService = currentWalletPositionService;
        _tradeRepositorySQL = tradeRepositorySQL;
        _fillsOrderService = fillsOrderService;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
    }

    public async Task SubscribeToWalletOrders(Wallet wallet)
    {
        await _orderProvider.SubscribeToNewOrders([wallet]);
    }

    public async Task SubscribeToTrackedWalletsOrders()
    {
        await _orderProvider.SubscribeToNewOrders(WalletSettings.TrackedWallets);
    }

    public async Task CollectHistoryOrders()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            var orders = await _walletInfo.GetHistoricalOrders(wallet);

            _orderDBProvider.WriteOrder(orders);

            _logger.LogInformation($"Исторические ордера для кошелька {wallet} собраны. {orders.Length}");
        }
    }

    private async void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var order in orders)
        {
            _logger.LogInformation($"Получен новый ордер: {order}");

            _orderSQLLiteRepository.WriteOrder(order);

            var minPeForOrder = await CalculateMinPerpEquityForOrder(order);
            _tradeRepositorySQL.WriteMinPeForOrder(minPeForOrder);
        }

        _fillsOrderService.OnNewOrders(orders);
    }

    public async Task<MinPEForOrder> CalculateMinPerpEquityForOrder(OriginalOrder order)
    {
        var walletInfo = await _walletInfo.GetInfo(order.Wallet);

        var minPE = CalculateMinPerpEquity(walletInfo.AccountVolume, order.VolumeUsd);
        var subType = _currentWalletPositionService.GetOrderSubType(order);

        return new MinPEForOrder
        {
            OrderId = order.OrderId,
            AccountVolume = walletInfo.AccountVolume,
            MinPE = minPE,
            SubType = subType,
        };
    }

    public async Task<decimal> CalculateMinPerpEquityForHystoryTrades(Wallet wallet)
    {
        var trades = await _walletInfo.GetHistoricalTrades(wallet);
        var walletInfo = await _walletInfo.GetInfo(wallet);

        var minPerpE = decimal.MinValue;
        //Тут надо получать Value Wallet в определенный момент времени ( трейда ) и вычислять исходя из него
        foreach (var t in trades.Take(100))
        {
            var tradeEquity = 100 / (t.VolumeUsd / walletInfo.AccountVolume * 100) * 10;
            if (tradeEquity > minPerpE)
            {
                minPerpE = tradeEquity;
            }
        }

        return minPerpE;
    }

    public decimal CalculateMinPerpEquity(decimal walletVolume, decimal tradeVolume)
    {
        return 100 / (tradeVolume / walletVolume * 100);
    }
}