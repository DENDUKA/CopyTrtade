using CopyTrading.Models.Enums;

namespace CopyTrading.Mappers;

public static class OrderTypeMapper
{
    public static OrderBuyType ToBll(this HyperLiquid.Net.Enums.OrderType orderType)
    {
        return orderType switch
        {
            HyperLiquid.Net.Enums.OrderType.Market => OrderBuyType.Market,
            HyperLiquid.Net.Enums.OrderType.Limit => OrderBuyType.Limit,
            _ => throw new ArgumentOutOfRangeException(nameof(orderType), orderType, null)
        };
    }
}