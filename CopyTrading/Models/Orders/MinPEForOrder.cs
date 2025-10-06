using CopyTrading.Models.Enums.Order;

namespace CopyTrading.Models.Orders;

public class MinPEForOrder
{
    public long OrderId { get; set; }
    public decimal AccountVolume { get; set; }
    public decimal MinPE { get; set; }
    public OrderSubType SubType { get; set; }
}