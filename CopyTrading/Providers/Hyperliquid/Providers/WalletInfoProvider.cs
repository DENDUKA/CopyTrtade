using CopyTrading.Mappers;
using CopyTrading.Models;
using CopyTrading.Models.Orders;
using CopyTrading.Models.Trade;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Objects.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class WalletInfoProvider(
    ILogger<WalletInfoProvider> _logger,
    IMemoryCache _cache)
{
    private readonly TimeSpan _cacheLiveTime = TimeSpan.FromSeconds(5);

    private readonly HyperLiquidRestClient _restClient = new();

    public async Task<WalletInfoModel?> GetInfo(string wallet)
    {
        if (_cache.TryGetValue(wallet, out WalletInfoModel? cached))
            return cached;

        HyperLiquidFuturesAccount data = null;

        try
        {
            var response = await _restClient.FuturesApi.Account.GetAccountInfoAsync(wallet);

            if (response.Success)
            {
                data = response.Data;
            }
            else
            {
                _logger.LogError(response.Error!.Message);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get Wallet Info Error");
            return null;
        }

        var result = data.ToBll(wallet);
        _cache.Set(wallet, result, _cacheLiveTime);
        return result;
    }

    public async Task<OriginalOrder[]> GetHistoricalOrders(string wallet)
    {
        var response = await _restClient.FuturesApi.Trading.GetOrderHistoryAsync(wallet);
        if (response.Success)
        {
            return response.Data.Select(x => x.ToBll(wallet)).ToArray();
        }

        _logger.LogError(response.Error!.Message);
        return Array.Empty<OriginalOrder>();
    }

    public async Task<OriginalTrade[]> GetHistoricalTrades(string wallet)
    {
        var response = await _restClient.FuturesApi.Trading.GetUserTradesAsync(wallet);

        if (response.Success)
        {
            return response.Data.Select(x => x.ToBll(wallet)).ToArray();
        }

        _logger.LogError(response.Error!.Message);
        return [];
    }
}
