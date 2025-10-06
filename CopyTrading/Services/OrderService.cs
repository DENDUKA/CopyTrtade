using CopyTrading.DataEvents;
using CopyTrading.Models;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using CopyTrading.Values;
using SQLLiteOrderRepository = CopyTrading.Repository.SQLite.OrderRepository;
using TradeRepositorySQL = CopyTrading.Repository.SQLite.TradeRepository;

namespace CopyTrading.Services;

public class OrderService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfo;
    private readonly OrderRepository _orderDBProvider;
    private readonly ExchangeInfoProvider _exchangeInfoProvider;
    private readonly SQLLiteOrderRepository _orderSQLLiteRepository;
    private readonly TradeRepositorySQL _tradeRepositorySQL;
    private readonly InformationService _informationService;
    private readonly CurrentWalletPositionService _currentWalletPositionService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfo,
        OrderRepository orderDBProvider,
        ExchangeInfoProvider exchangeInfoProvider,
        SQLLiteOrderRepository orderSQLLiteProvider,
        TradeRepositorySQL tradeRepositorySQL,
        InformationService informationService,
        CurrentWalletPositionService currentWalletPositionService,
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

    private void OnNewOrders(OriginalOrder[] orders)
    {
        //_orderDBProvider.WriteOrder(obj);

        foreach (var order in orders)
        {
            _logger.LogInformation($"Получен новый ордер: {order}");

            _orderSQLLiteRepository.WriteOrder(order);

            CalculateWriteMinPerpEquityForOrder(order);
        }
    }

    private async Task CalculateWriteMinPerpEquityForOrder(OriginalOrder order)
    {
        var walletInfo = await _walletInfo.GetInfo(order.Wallet);

        var minPE = _informationService.CalculateMinPerpEquity(walletInfo.AccountVolume, order.VolumeUsd);
        var subType = await _currentWalletPositionService.GetOrderSubType(order);

        _tradeRepositorySQL.WriteMinPeForOrder(new MinPEForOrder
        {
            OrderId = order.OrderId,
            AccountVolume = walletInfo.AccountVolume,
            MinPE = minPE,
            SubType = subType,
        });
    }

    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Size
    /// </summary>
    private async Task CorrectPlacedOrder(CopyOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        order.Quantity = Math.Round(order.Quantity, exchangeInfo.QuantityDecimals!.Value);
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