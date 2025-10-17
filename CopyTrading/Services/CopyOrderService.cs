using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для непостредственного копирования ордеров и размещения на бирже
/// </summary>
public class CopyOrderService
{
    private readonly OrderService _orderService;
    private readonly IWalletInfoProvider _walletProvider;
    private readonly ExchangeInfoProvider _exchangeInfoProvider;
    private readonly ILogger<CopyOrderService> _logger;
    //TODO вынести в конструктор , задаваться должен для каждого экземпляра ( поменять singleton )
    private readonly Wallet _myWallet = WalletSettings.MyWallet;

    public CopyOrderService(
        OrderService orderService,
        IWalletInfoProvider walletProvider,
        ExchangeInfoProvider exchangeInfoProvider,
        ILogger<CopyOrderService> logger)
    {
        _orderService = orderService;
        _walletProvider = walletProvider;
        _exchangeInfoProvider = exchangeInfoProvider;
        _logger = logger;

        DataBusEvents.NewOrders += OnNewOrders;
    }

    private void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var order in orders)
        {
            OnNewOrder(order);
        }
    }

    private void OnNewOrder(OriginalOrder order)
    {
        switch (order.Status)
        {
            case OrderStatus.Open:
                {
                    OrderOpen(order);
                    break;
                }
            case OrderStatus.Filled:
                {
                    OrderFilled(order);
                    break;
                }
            case OrderStatus.Canceled:
                {
                    OrderCanceled(order);
                    break;
                }
            default:
                {
                    _logger.LogError($"CopyOrderService OnNewOrder: Неизвестный статус ордера {order.Status}");
                    break;
                }
        }
    }

    /// <summary>
    /// Ордер открыт 
    /// Проверить нет ли у нас уже копии этого ордера (маловероятно)
    /// Проверить можем ли мы его разместить ( достаточно ли средств, маржи, минимальной суммы размещения)
    /// Создать копию ордера и поправить его по округлению quantity и тд
    /// Разместить его на бирже 
    /// Записать в БД о том что мы разместили и для какого originalOrderId его копировали
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    private async Task OrderOpen(OriginalOrder order)
    {
        if (order.Status != OrderStatus.Open) return;

        var minPeForOrder = await _orderService.CalculateMinPerpEquityForOrder(order);
        var walletInfo = await _walletProvider.GetInfo(_myWallet, false);

        var copyOrder = await CreateCopyOrder(order);

        _logger.LogInformation($"CopyOrderService OrderOpen Создан копируемый ордер: {copyOrder}");
    }

    private void OrderCanceled(OriginalOrder order)
    {
        //throw new NotImplementedException();
    }

    private void OrderFilled(OriginalOrder order)
    {
        //throw new NotImplementedException();
    }

    private async Task<CopyOrderV2> CreateCopyOrder(OriginalOrder order)
    {
        var defaultLevel = 5;
        var myAccountValue = 2000M;

        var walletInfo = await _walletProvider.GetInfo(order.Wallet);

        var orderRatio = order.VolumeUsd / walletInfo.AccountVolume;

        //TODO Подумать как получать Получить Leverage CurrentWalletPositionService.GetLEverage(Wallet, Coin)
        //Либо просто задается один раз для всех Coin или вообще это делать не тут а при размещении ордера

        var newCopyOrder = new CopyOrderV2()
        {
            OriginalOrder = order,
            OrderId = order.OrderId,
            OrderRatio = orderRatio,
            MyPE = myAccountValue,
            AccountPE = walletInfo.AccountVolume,
            Quantity = order.Quantity * orderRatio,
        };

        await CorrectCopyOrder(newCopyOrder);

        return newCopyOrder;
    }

    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Quantity
    /// </summary>
    private async Task CorrectCopyOrder(CopyOrderV2 copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.OriginalOrder.Symbol);

        copyOrder.Quantity = Math.Round(copyOrder.Quantity, exchangeInfo.QuantityDecimals!.Value, MidpointRounding.ToPositiveInfinity);
    }

    private async Task<bool> ValidatePLacedOrder(CopyOrder copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.Symbol);

        if (copyOrder.VolumeUsd < exchangeInfo.MinNotionalValue.Value)
        {
            return false;
        }

        if (copyOrder.Quantity < exchangeInfo.MinTradeQuantity.Value)
        {
            return false;
        }

        return true;
    }
}
