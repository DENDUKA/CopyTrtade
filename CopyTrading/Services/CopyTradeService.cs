using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

public class CopyTradeService(
            IWalletInfoProvider _walletInfo,
            ExchangeInfoProvider _exchangeInfoProvider)
{
    private readonly OrderSubType[] OrderOpenedTypes = [OrderSubType.Decrease, OrderSubType.Increase, OrderSubType.Close];







    /// <summary>
    /// Корректируем размещаемый ордер 
    /// 1) по количесву разрешенных знаков после запятой у Size
    /// </summary>
    private async Task CorrectPlacedOrder(CopyOrder order)
    {
        var exchangeInfo = await _exchangeInfoProvider.GetExchangeInfo(order.Symbol);

        order.Quantity = Math.Round(order.Quantity, exchangeInfo.QuantityDecimals!.Value);
    }

    private static OrderSubType GetOrderType(Dictionary<string, Position> positions, OriginalTrade order)
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
