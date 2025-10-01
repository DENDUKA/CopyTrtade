using CopyTrading.Models;
using CopyTrading.Models.Enums.Order;
using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

public class CopyTradeService(
            IWalletInfoProvider _walletInfo,
            ExchangeInfoProvider _exchangeInfoProvider)
{
    private readonly OrderSubType[] OrderOpenedTypes = [OrderSubType.Decrease, OrderSubType.Increase, OrderSubType.Close];

    public async Task CreateMerketTrade(Trade trade)
    {
        var copyOrder = await CreateCopyOrder(trade, OrderType.Market);
    }

    private async Task<CopyOrder> CreateCopyOrder(Trade trade, OrderType marketType)
    {
        var defaultLevel = 5;
        var myAccountValue = 1000;

        var walletInfo = await _walletInfo.GetInfo(trade.Wallet);
        //null ответ GetInfo по идее не должно быть такого

        var orderType = GetOrderType(walletInfo.Positions, trade);

        var orderRatio = trade.VolumeUsd / walletInfo.AccountVolume;
        var leverage = OrderOpenedTypes.Contains(orderType) ? walletInfo.Positions[trade.Symbol].Leverage : defaultLevel;
        var orderSize = myAccountValue * orderRatio / trade.Price;

        var newCopyOrder = new CopyOrder()
        {
            OrderId = trade.TradeId,
            Symbol = trade.Symbol,
            Direction = trade.Direction,            
            Leverage = leverage,
            SubType = trade.SubType,
            Price = trade.Price,
            Size = orderSize,
            Wallet = trade.Wallet,
            OrderRatio = orderRatio * 100M,
        };

        await CorrectPlacedOrder(newCopyOrder);

        return newCopyOrder;
    }

    private async Task<bool> ValidatePLacedOrderAsync(CopyOrder copyOrder)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(copyOrder.Symbol);

        if (copyOrder.Value < exchangeInfo.MinNotionalValue.Value)
        {
            return false;
        }

        if (copyOrder.Size < exchangeInfo.MinTradeQuantity.Value)
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
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        order.Size = Math.Round(order.Size, exchangeInfo.QuantityDecimals!.Value);
    }

    private static OrderSubType GetOrderType(Dictionary<string, Position> positions, Trade order)
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
