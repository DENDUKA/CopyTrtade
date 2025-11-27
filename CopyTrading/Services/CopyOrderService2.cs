using CopyTrading.DataEvents;
using CopyTrading.Extensions;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using CryptoExchange.Net.SharedApis;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService2(
    IWalletInfoProvider _walletProvider,
    IExchangeInfoProvider _exchangeInfoProvider,
    ICurrentWalletPositionService _currentWalletPositionService,
    IPositionMappingService _positionMappingService,
    ICopyOrderResultService _resultService,
    IFillsOrderService _fillsOrderService,
    ICopyTradeWalletSettingsService _walletSettingsService,
    ILogger<CopyOrderService> _logger) : ICopyOrderService
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
        if (order.Type == OrderType.None)
        {
            _logger.LogWarning($"CopyOrderService.OnNewOrder: Получен ордер с типом None. Ордер пропущен. OrderId: {order.OrderId}");
            return;
        }

        if (order.Type == OrderType.Market)
        {
            
        }

    }
}
