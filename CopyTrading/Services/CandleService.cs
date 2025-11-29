using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Repository.Influx.Interfaces;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

public class CandleService(
    CandlesProvider _candlesProvider,
    ICandlesRepository _candlesRepository) : ICandleService
{
    public async Task CollectCandles(string symbol)
    {
        var candels = await _candlesProvider.GetHystoryFuturesCandles(symbol);

        _candlesRepository.WriteCandles(candels);
    }
}
