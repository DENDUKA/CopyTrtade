using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;

namespace CopyTrading.DataEvents;

public static class DataBusEvents
{
    public static Action<(OriginalTrade[] Trades, bool IsSnapshot)> NewTrades;
    public static Action<OriginalOrder[]> NewOrders;
    public static Action<OrderFills> OrderFinished;
}
