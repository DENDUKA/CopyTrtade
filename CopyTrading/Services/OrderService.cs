using CopyTrading.Models;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Settings;

using SQLLiteOrderRepository = CopyTrading.Repository.SQLite.OrderRepository;

namespace CopyTrading.Services;

public class OrderService(
        OrdersTradesSubscriber _orderProvider,
        WalletInfoProvider _walletInfo,
        OrderRepository _orderDBProvider,
        ExchangeInfoProvider _exchangeInfoProvider,
        SQLLiteOrderRepository _orderSQLLiteProvider,
        ILogger<OrderService> _logger)
{
    private readonly OrderSubType[] OrderOpenedTypes = [OrderSubType.Decrease, OrderSubType.Increase, OrderSubType.Close];

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
            //var copyOrder = await CreateCopyOrder(order);

            //var validateResult = await ValidatePLacedOrderAsync(copyOrder);

            //copyOrder.OriginalOrder = order;

            //if (validateResult)
            //{
            //    _logger.LogInformation($"Размещаем ордер : {copyOrder}");
            //    await _orderSQLLiteProvider.AddOrder(copyOrder, OrderStatus.Open);
            //}
            //else
            //{
            //    _logger.LogWarning($"Не удалось разместить ордер для {order.Wallet}: {order.Id}");
            //}
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



    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Size
    /// </summary>
    private async Task CorrectPlacedOrder(CopyOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Coin);

        order.Size = Math.Round(order.Size, exchangeInfo.QuantityDecimals!.Value);
    }

    private static OrderSubType GetOrderType(Dictionary<string, PositionModel> positions, OriginalOrder order)
    {
        if (positions.ContainsKey(order.Coin))
        {
            if (positions[order.Coin].Direction == order.Direction)
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

    #endregion
}