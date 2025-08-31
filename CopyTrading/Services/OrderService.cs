using CopyTrading.Models;
using CopyTrading.Models.Enums;
using CopyTrading.Models.Orders;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Settings;

namespace CopyTrading.Services;

public class OrderService
{
    private readonly OrdersTradesSubscriber _orderProvider;
    private readonly WalletInfoProvider _walletInfo;
    private readonly OrderRepository _orderDBProvider;
    private readonly ExchangeInfoProvider _exchangeInfoProvider;
    private readonly Repository.SQLite.OrderRepository _orderSQLLiteProvider;

    private readonly ILogger<OrderService> _logger;

    private readonly OrderType[] OrderOpenedTypes = [OrderType.Decrease, OrderType.Increase, OrderType.Close];

    public OrderService(
        OrdersTradesSubscriber orderProvider,
        WalletInfoProvider walletInfo,
        OrderRepository orderDBProvider,
        ExchangeInfoProvider exchangeInfoProvider,
        Repository.SQLite.OrderRepository orderSQLLiteProvider,
        ILogger<OrderService> logger)
    {
        _orderProvider = orderProvider;
        _walletInfo = walletInfo;
        _orderDBProvider = orderDBProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _orderSQLLiteProvider = orderSQLLiteProvider;
        _logger = logger;
    }

    public async Task SubscribeToWalletOrders(string wallet)
    {
        await _orderProvider.SubscribeToNewOrders(wallet, NewOrders);
    }

    public async Task SubscribeToTrackedWalletsOrders()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            await _orderProvider.SubscribeToNewOrders(wallet, NewOrders);
        }
    }

    public async Task CollectHistoryOrders()
    {
        foreach (var wallet in WalletSettings.TrackedWallets)
        {
            var orders = await _walletInfo.GetHistoricalOrders(wallet);

            _orderDBProvider.WriteOrder(orders);

            Console.WriteLine($"Исторические ордера для кошелька {wallet} собраны. {orders.Length}");
        }
    }

    #region Orders

    private void NewOrders(OriginalOrder[] obj)
    {
        //_orderDBProvider.WriteOrder(obj);

        foreach (var order in obj)
        {
            Console.WriteLine(order);


            switch (order.Status)
            {
                case OrderStatus.Canceled:
                    CancelOrder(order);
                    break;
                case OrderStatus.Open:
                    OpenOrder(order);
                    break;
                case OrderStatus.Filled:
                    FilledOrder(order);
                    break;
                default:
                    Console.WriteLine($"Не известный статус размещаемого ордера {order.Status}");
                    break;
            }
        }
    }

    private async Task OpenOrder(OriginalOrder order)
    {
        try
        {
            var copyOrder = await CreateCopyOrder(order);

            var validateResult = await ValidatePLacedOrderAsync(copyOrder);

            copyOrder.OriginalOrder = order;

            if (validateResult)
            {
                _logger.LogInformation($"Размещаем ордер : {copyOrder}");
                await _orderSQLLiteProvider.AddOrder(copyOrder, OrderStatus.Open);
            }
            else
            {
                _logger.LogWarning($"Не удалось разместить ордер для {order.Wallet}: {order.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task FilledOrder(OriginalOrder order)
    {
        _orderSQLLiteProvider.ChangeOrderStatus(order.Id, OrderStatus.Filled);
    }

    private async Task CancelOrder(OriginalOrder order)
    {
        _orderSQLLiteProvider.RemoveOrder(order.Id);
    }

    private async Task<CopyOrder> CreateCopyOrder(OriginalOrder order)
    {
        var defaultLevel = 5;
        var myAccountValue = 1000;

        var walletInfo = await _walletInfo.GetInfo(order.Wallet);
        //null ответ GetInfo по идее не должно быть такого

        var orderType = GetOrderType(walletInfo.Positions, order);

        var orderRatio = order.Value / walletInfo.AccountValue;
        var leverage = OrderOpenedTypes.Contains(orderType) ? walletInfo.Positions[order.Coin].Leverage : defaultLevel;
        var orderSize = myAccountValue * orderRatio / order.Price;

        var newOrder = new CopyOrder()
        {
            Coin = order.Coin,
            Direction = order.Direction,
            Id = order.Id,
            Leverage = leverage.Value,
            OrderType = order.OrderType,
            Price = order.Price,
            Size = orderSize,
            Wallet = order.Wallet,
            OrderRatio = orderRatio * 100,
        };

        await CorrectPlacedOrder(newOrder);

        return newOrder;
    }

    private async Task<bool> ValidatePLacedOrderAsync(CopyOrder copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.Coin);

        if (copyOrder.Value < (double)exchangeInfo.MinNotionalValue.Value)
        {
            return false;
        }

        if (copyOrder.Size < (double)exchangeInfo.MinTradeQuantity.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Size
    /// </summary>
    private async Task CorrectPlacedOrder(CopyOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Coin);

        order.Size = Math.Round(order.Size, exchangeInfo.QuantityDecimals!.Value);
    }

    private static OrderType GetOrderType(Dictionary<string, PositionModel> positions, OriginalOrder order)
    {
        if (positions.ContainsKey(order.Coin))
        {
            if (positions[order.Coin].Direction == order.Direction)
            {
                return OrderType.Increase;
            }
            else
            {
                return OrderType.Decrease;
            }
        }
        else
        {
            return OrderType.Open;
        }
    }

    #endregion
}