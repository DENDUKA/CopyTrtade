using CopyTrading.Models.Models.Enums.Order;

namespace CopyTrading.Repository.SQLite.Dto;

public class MinPerpEquityForTradesQuery
{
    public OrderSubType[] SubTypes { get; set; }
}