using CopyTrading.Models.Models.Trade;

namespace CopyTrading.Repository.Influx.Interfaces;

public interface ITradeRepository
{
    void WriteTrades(OriginalTrade[] trades);
}
