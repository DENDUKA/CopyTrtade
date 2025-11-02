using CopyTrading.DataEvents;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
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
    private readonly InformationService _informationService;
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
        InformationService informationService,
        CurrentWalletPositionService currentWalletPositionService,
        FillsOrderService fillsOrderService,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _orderSQLLiteRepository = orderSQLLiteProvider;
        _informationService = informationService;
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

        var minPE = _informationService.CalculateMinPerpEquity(walletInfo.AccountVolume, order.VolumeUsd);
        var subType = _currentWalletPositionService.GetOrderSubType(order);

        return new MinPEForOrder
        {
            OrderId = order.OrderId,
            AccountVolume = walletInfo.AccountVolume,
            MinPE = minPE,
            SubType = subType,
        };
    }



    private static OrderSubType GetOrderType(Dictionary<string, Position> positions, OriginalOrder order)
    {
        if (positions.ContainsKey(order.Symbol))
        {
            if (positions[order.Symbol].Direction == order.Direction)
            {
                return OrderSubType.Increase;
            }
            else
            {
                return OrderSubType.Decrease;
            }
        }
        else
        {
            return OrderSubType.Open;
        }
    }
}