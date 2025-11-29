using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Providers.Hyperliquid.Interfaces;

public interface IOrdersProvider
{
    public Task<OrderPlaceResult> Place(CopyOrder order);

    public Task<OrderPlaceResult> Close(long orderId);

    /// <summary>
    /// Только длдя тестирования
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public Task<OrderPlaceResult> Filled(long orderId);
}
