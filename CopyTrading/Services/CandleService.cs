using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Repository.Influx;

namespace CopyTrading.Services;

public class CandleService(
    CandlesProvider _candlesProvider,
    CandlesRepository _candlesRepository)
{
    public async Task CollectCandles(string symbol)
    {
        var candels = await _candlesProvider.GetHystoryFuturesCandles(symbol);

        _candlesRepository.WriteCandles(candels);
    }
}
