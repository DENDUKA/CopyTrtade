using CopyTrading.Models.Models;

namespace CopyTrading.Repository.Influx;

public interface ICandlesRepository
{
    void WriteCandles(Candle[] candles);
}
