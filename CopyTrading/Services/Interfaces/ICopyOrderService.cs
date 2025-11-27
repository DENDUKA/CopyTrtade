using CopyTrading.Models.Models.Orders;

namespace CopyTrading.Services.Interfaces;

public interface ICopyOrderService
{
    Task OnNewOrders(OriginalOrder[] orders);
}
