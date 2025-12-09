using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Repository.SQLite.Dto;

namespace CopyTrading.Repository.SQLInterfaces.Interfaces;

public interface ITradeRepository
{
    Task WriteTrade(OriginalTrade trade);
    Task WriteMinPeForTrade(MinPEForTrade dto);
    Task WriteMinPeForTrade(MinPEForTrade[] dtos);
    Task<MinPEForTrade[]> MinPerpEquityForTradesQuery(MinPerpEquityForTradesQuery minPerpEquityForTradesQuery);
    Task WriteMinPeForOrder(MinPEForOrder minPeForOrder);
}
