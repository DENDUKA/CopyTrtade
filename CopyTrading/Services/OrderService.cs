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

public class OrderService(
        OrdersTradesSubscriber _orderProvider,
        IWalletInfoProvider _walletInfo,
        OrderRepository _orderDBProvider,
        ExchangeInfoProvider _exchangeInfoProvider,
        SQLLiteOrderRepository _orderSQLLiteProvider,
        
        ILogger<OrderService> _logger)
{
    private readonly OrderSubType[] OrderOpenedTypes = [OrderSubType.Decrease, OrderSubType.Increase, OrderSubType.Close];

    public async Task SubscribeToWalletOrders(Wallet wallet)
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

    private void NewOrders(OriginalOrder[] orders)
    {
        //_orderDBProvider.WriteOrder(obj);

        foreach (var order in orders)
        {
            _logger.LogInformation($"Получен новый ордер: {order}");

            _orderSQLLiteProvider.WriteOrder(order);

            switch (order.Status)
            {
                case OrderStatus.Canceled:

                    break;
                case OrderStatus.Open:
                    OpenOrder(order);
                    break;
                case OrderStatus.Filled:

                    break;
                default:
                    _logger.LogError($"Не известный статус размещаемого ордера {order.Status}");
                    break;
            }
        }
    }

    private async Task OpenOrder(OriginalOrder order)
    {
        try
        {

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }


    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Size
    /// </summary>
    private async Task CorrectPlacedOrder(CopyOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        order.Size = Math.Round(order.Size, exchangeInfo.QuantityDecimals!.Value);
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

    #endregion
}