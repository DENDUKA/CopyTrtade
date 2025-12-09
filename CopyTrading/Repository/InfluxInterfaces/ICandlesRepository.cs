using CopyTrading.Models.Models;

namespace CopyTrading.Repository.InfluxInterfaces;

public interface ICandlesRepository
{
    void WriteCandles(Candle[] candles);
}
