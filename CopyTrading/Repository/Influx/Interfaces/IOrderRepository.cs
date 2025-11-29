using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Repository.Influx.Interfaces;

public interface IOrderRepository
{
    void WriteOrder(OriginalOrder[] orders);
}
