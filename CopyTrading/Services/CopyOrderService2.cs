using CopyTrading.DataEvents;
using CopyTrading.Extensions;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using CryptoExchange.Net.SharedApis;
using System.Threading.Tasks;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService2(
    IActiveWindowService _activeWindowService,
    ILogger<CopyOrderService2> _logger) : ICopyOrderService
{
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public async Task OnNewOrders(OriginalOrder[] orders)
    {
        ArgumentNullException.ThrowIfNull(orders, nameof(orders));

        try
        {
            foreach (var order in orders)
            {
                await OnNewOrder(order);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CopyOrderService.OnNewOrders: Ошибка при обработке массива ордеров");
        }
    }

    private async Task OnNewOrder(OriginalOrder order)
    {
        switch (order.Type)
        {
            case Models.Models.Enums.Order.OrderType.None:
                _logger.LogWarning($"CopyOrderService.OnNewOrder: Получен ордер с типом None. Ордер пропущен. OrderId: {order.OrderId}");
                return;

            case Models.Models.Enums.Order.OrderType.Market:
                NewMarketOrderHandler(order);
                break;

            case Models.Models.Enums.Order.OrderType.Limit:
                NewLimitOrderHandler(order);
                break;
        }
    }

    private async Task NewLimitOrderHandler(OriginalOrder order)
    {
        if (order.Direction == Direction.None)
        {
            _logger.LogWarning($"CopyOrderService2.NewLimitOrderHandler: Ордер с Direction.None пропущен. OrderId={order.OrderId}");
            return;
        }

        var nearestOrders = _activeWindowService.GetNearestOrders(order.Wallet, order.Symbol, order.Direction, count: 2);

        // Проверяем, есть ли наш ордер среди ближайших
        var isOurOrderInNearestOrders = nearestOrders.Any(o => o.OriginalOrder.OrderId == order.OrderId);

        if (!isOurOrderInNearestOrders)
        {
            _logger.LogInformation(
                $"CopyOrderService2.NewLimitOrderHandler: Ордер OrderId={order.OrderId} НЕ находится среди {nearestOrders.Length} ближайших. " +
                $"Wallet={order.Wallet}, Symbol={order.Symbol}, Direction={order.Direction}, Price={order.Price}. Пропускаем.");
            return;
        }



    }

    private void NewMarketOrderHandler(OriginalOrder order)
    {
        throw new NotImplementedException();
    }
}
