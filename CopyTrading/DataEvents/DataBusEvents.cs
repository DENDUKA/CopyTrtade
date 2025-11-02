using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;

namespace CopyTrading.DataEvents;

public static class DataBusEvents
{
    public static Action<(OriginalTrade[] Trades, bool IsSnapshot)> NewTrades;
    public static Action<OriginalOrder[]> NewOrders;
    public static Action<OrderFills> OrderFinished;

    /// <summary>
    /// Событие создания нового копируемого ордера
    /// </summary>
    public static Action<CopyOrderV2> CopyOrderCreated;

    /// <summary>
    /// Событие закрытия копируемого ордера (Canceled, Rejected)
    /// Параметры: (OriginalOrder, OrderStatus)
    /// </summary>
    public static Action<(OriginalOrder Order, OrderStatus Status)> CopyOrderClosed;

    public static Action<(OriginalOrder Order, OrderStatus Status)> CopyOrderFilled;
}
