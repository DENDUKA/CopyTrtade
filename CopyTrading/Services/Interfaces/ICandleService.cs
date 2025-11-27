namespace CopyTrading.Services.Interfaces;

public interface ICandleService
{
    Task CollectCandles(string symbol);
}
