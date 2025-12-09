using CopyTrading.Models.Models.Trade;

namespace CopyTrading.Repository.InfluxInterfaces;

public interface ITradeRepository
{
    void WriteTrades(OriginalTrade[] trades);
}
