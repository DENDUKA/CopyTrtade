using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Options;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class OrdersProvider 
{
    private readonly HyperLiquidRestClient _hyperLiquidRestClient;
    private readonly ILogger<OrdersProvider> _logger;

    //Key Secret вынести в secret.json
    private readonly string key = "1";
    private readonly string secret = "1";

    public OrdersProvider(ILogger<OrdersProvider> logger)
    {
        _hyperLiquidRestClient = new HyperLiquidRestClient(new Action<HyperLiquidRestOptions>(options =>
        {
            options.ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(key, secret);
        }));
        _logger = logger;
    }

    public async Task<long?> PlaceOrder()
    {
        try
        {
            var response = await _hyperLiquidRestClient.FuturesApi.Trading.PlaceOrderAsync("ETH", OrderSide.Buy, OrderType.Market, 1, 4400);

            if (response.Success)
            {
                return response.Data.OrderId;
            }
            else
            {
                _logger.LogError($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NewOrder Exception {ex.Message}");
        }

        return null;
    }

    public async Task<bool> CloseOrder(string symbol, long orderId)
    {
        try
        {
            var response = await _hyperLiquidRestClient.FuturesApi.Trading.CancelOrderByClientOrderIdAsync(symbol, orderId.ToString());

            if (response is not null && response.Success)
            {
                //TODO надо получить ответ
                return true;
            }
            else
            {
                _logger.LogError($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"NewOrder Exception {ex.Message}");
        }

        return false;
    }

}
