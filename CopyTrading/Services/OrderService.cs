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

namespace CopyTrading.Services;

public class OrderService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly IWalletInfoProvider _walletInfo;
    private readonly OrderRepository _orderDBProvider;
    private readonly ExchangeInfoProvider _exchangeInfoProvider;
    private readonly SQLLiteOrderRepository _orderSQLLiteProvider;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrdersTradesSubscriber orderProvider,
        IWalletInfoProvider walletInfo,
        OrderRepository orderDBProvider,
        ExchangeInfoProvider exchangeInfoProvider,
        SQLLiteOrderRepository orderSQLLiteProvider,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _orderSQLLiteProvider = orderSQLLiteProvider;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
    }

    public async Task SubscribeToWalletOrders(Wallet wallet)
    {
        await _orderProvider.SubscribeToNewOrders(wallet, DataBusEvents.NewOrders);
    }

    public async Task SubscribeToTrackedWalletsOrders()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            await _orderProvider.SubscribeToNewOrders(wallet, DataBusEvents.NewOrders);
        }
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

            _orderSQLLiteProvider.WriteOrder(order);
        }
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