namespace CopyTrading.Models.Enums.Order;

public enum OrderStatus
{
    Unknown,
    Open,
    Filled,
    Canceled,
    Triggered,
    Rejected,
    MarginCanceled
}