using CopyTrading.Providers.Hyperliquid.Interfaces;
using CryptoExchange.Net.SharedApis;
using HyperLiquid.Net.Clients;
using Microsoft.Extensions.Caching.Memory;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class ExchangeInfoProvider(
    ILogger<ExchangeInfoProvider> _logger,
    IMemoryCache _cache) : IExchangeInfoProvider
{
    private readonly HyperLiquidRestClient _client = new();

    public async Task<SharedFuturesSymbol> GetExchangeInfo(string symbol)
    {
        if (_cache.TryGetValue(symbol, out SharedFuturesSymbol cached))
        {
            return cached;
        }
        else
        {
            await CacheExchangeInfo();

            if (_cache.TryGetValue(symbol, out cached))
            {
                return cached;
            }
            else
            {
                _logger.LogError($"Не найдена информация о {symbol}");
                return null;
            }
        }
    }

    public async Task<SharedFuturesSymbol[]> GetExchangeInfo()
    {
        var response = await _client.FuturesApi.ExchangeData.GetExchangeInfoAsync();

        var result = response.Data.Select(s => new SharedFuturesSymbol(TradingMode.PerpetualLinear, s.Name, "USDC", s.Name + "/USDC", true)
        {
            MinTradeQuantity = 1m / (decimal)Math.Pow(10, s.QuantityDecimals),
            MinNotionalValue = 10, // Order API returns error mentioning at least 10$ order value, but value isn't returned by symbol API
            QuantityDecimals = s.QuantityDecimals,
            PriceSignificantFigures = 5,
            PriceDecimals = 6 - s.QuantityDecimals,
            MaxLongLeverage = s.MaxLeverage,
            MaxShortLeverage = s.MaxLeverage
        }).ToArray();

        return result;
    }

    private async Task CacheExchangeInfo()
    {
        var result = await GetExchangeInfo();

        foreach (var item in result)
        {
            if (!_cache.TryGetValue(item.BaseAsset, out _))
            {
                _cache.Set(item.BaseAsset, item);
            }
        }
    }
}