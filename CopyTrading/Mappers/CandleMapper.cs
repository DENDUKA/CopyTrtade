using CopyTrading.Models.Models;
using CopyTrading.ProviderModels.InfluxDB;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class CandleMapper
{
    public static Candle ToBll(this HyperLiquidKline hlKline)
    {
        return new Candle()
        {
            Open = hlKline.OpenPrice,
            High = hlKline.HighPrice,
            Low = hlKline.LowPrice,
            Close = hlKline.ClosePrice,
            Symbol = hlKline.Symbol,
            Volume = hlKline.Volume,
            Time = hlKline.OpenTime,
            Interval = (int)hlKline.Interval / 60,
        };
    }

    public static CandleMeasurement ToMeasurement(this Candle candle)
    {
        return new CandleMeasurement
        {
            Close = candle.Close,
            High = candle.High,
            Low = candle.Low,
            Open = candle.Open,
            Symbol = candle.Symbol,
            Interval = candle.Interval,
            Volume = candle.Volume,
            Time = candle.Time,
        };
    }
}