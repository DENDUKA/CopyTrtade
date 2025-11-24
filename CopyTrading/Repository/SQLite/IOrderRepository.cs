using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Repository.SQLite;

public interface IOrderRepository
{
    Task WriteOrder(OriginalOrder order);
}
