using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using System.Collections.Concurrent;

namespace CopyTrading.Models.Models.Enums.Order;

public record OrderFills
{
    public OrderFills(OriginalOrder order)
    {
        OriginalOrder = order;
    }

    public OriginalOrder OriginalOrder { get; init; }
    public ConcurrentBag<OriginalTrade> Trades { get; init; } = [];
    public decimal FilledQuantity => Trades.Sum(x => x.Quantity);
    public OrderFillsStatus FillStatus
    {
        get
        {
            if (Trades.Count == 0) return OrderFillsStatus.NoFills;
            if (FilledQuantity == OriginalOrder.Quantity) return OrderFillsStatus.Filled;
            return OrderFillsStatus.PartiallyFilled;
        }
    }
}