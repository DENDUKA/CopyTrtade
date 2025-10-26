using CopyTrading.Models.Models.Enums.Order;

namespace CopyTrading.Models.Models.Orders;

public class CopyOrderV2
{
    public long OrderId {  get; set; }
    public long OriginalOrderId {  get; init; }
    public OriginalOrder OriginalOrder {  get; init; }
    public OrderSubType OrderSubType { get; init; }
    public decimal OrderRatio { get; init; }
    public decimal MyPE { get; init; }
    public decimal AccountPE { get; init; }
    public decimal Quantity { get; set; }

    public decimal VolumeUsd => Quantity * OriginalOrder.Price;

    public override string ToString()
    {
        return $"CopyOrder Oo.Q:{OriginalOrder.Quantity} CopyQ:{Quantity} Oo.V:{OriginalOrder.VolumeUsd} CopyV:{VolumeUsd} " +
               $"ORation:{OrderRatio} MyValue:{MyPE} CopyValue:{AccountPE}";
    }
}