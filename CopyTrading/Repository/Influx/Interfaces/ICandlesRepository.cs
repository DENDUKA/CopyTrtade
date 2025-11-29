using CopyTrading.Models.Models;

namespace CopyTrading.Repository.Influx.Interfaces;

public interface ICandlesRepository
{
    void WriteCandles(Candle[] candles);
}
