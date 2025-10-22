using CryptoExchange.Net.SharedApis;

namespace CopyTrading.Providers.Hyperliquid.Interfaces;

public interface IExchangeInfoProvider
{
    Task<SharedFuturesSymbol> GetExchangeInfo(string symbol);
    Task<SharedFuturesSymbol[]> GetExchangeInfo();
}
