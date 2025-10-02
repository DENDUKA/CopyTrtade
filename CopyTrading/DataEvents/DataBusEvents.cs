using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;

namespace CopyTrading.DataEvents;

public static class DataBusEvents
{
    public static Action<(OriginalTrade[] Trades, bool IsSnapshot)> NewTrades;
    public static Action<OriginalOrder[]> NewOrders;
}
