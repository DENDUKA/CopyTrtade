using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Repository.SQLInterfaces.Interfaces;

public interface IOrderRepository
{
    Task WriteOrder(OriginalOrder order);
}
