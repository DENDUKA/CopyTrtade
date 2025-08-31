using CopyTrading.Mappers;
using CopyTrading.Models;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class CandlesProvider
{
    private readonly HyperLiquidRestClient _restClient = new();

    public async Task<Candle[]> GetHystoryFuturesCandles(string symbol)
    {
        var endTime = DateTime.Now;
        var startTime = endTime.AddDays(-30);

        var klines = await _restClient.FuturesApi.ExchangeData.GetKlinesAsync(
            symbol,
            KlineInterval.FiveMinutes,
            startTime, 
            endTime);

        if (klines.Success)
        {
            return [.. klines.Data.Select(k => k.ToBll())];
        }

        Console.WriteLine($"Не удалось получить данные по японским свечам");

        return [];
    }
}
