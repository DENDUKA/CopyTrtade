using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Repository.Influx;

public interface IOrderRepository
{
    void WriteOrder(OriginalOrder[] orders);
}
