using CopyTrading.Models.Models.Trade;

namespace CopyTrading.Repository.Influx;

public interface ITradeRepository
{
    void WriteTrades(OriginalTrade[] trades);
}
