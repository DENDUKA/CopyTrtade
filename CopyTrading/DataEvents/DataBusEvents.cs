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

    public static Action<(OriginalOrder Order, OrderStatus Status)> CopyOrderFilled;

    /// <summary>
    /// Очистить все подписки на события (используется для тестирования)
    /// ВНИМАНИЕ: После вызова этого метода все сервисы должны быть пересозданы
    /// для повторной подписки на события!
    /// </summary>
    public static void ClearAllSubscriptions()
    {
        NewTrades = null;
        NewOrders = null;
        OrderFinished = null;
        CopyOrderCreated = null;
        CopyOrderFilled = null;
    }
}
