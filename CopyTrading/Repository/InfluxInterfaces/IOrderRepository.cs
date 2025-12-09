using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Repository.InfluxInterfaces;

public interface IOrderRepository
{
    void WriteOrder(OriginalOrder[] orders);
}
