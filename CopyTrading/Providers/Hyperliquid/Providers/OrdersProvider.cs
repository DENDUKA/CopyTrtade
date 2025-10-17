using CopyTrading.Models.Models.Orders;
using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Options;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class OrdersProvider 
{
    private readonly HyperLiquidRestClient _hyperLiquidRestClient;
    private readonly ILogger<OrdersProvider> _logger;

    //TODO Key Secret вынести в secret.json
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

    public async Task<long?> UpdateLeverage(NewLeverageModel newLeverage)
    {
        try
        {
            //var response = await _hyperLiquidRestClient.FuturesApi.Trading.SetLeverageAsync(,);
            //TODO
            //https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint#update-leverage
            //нужен api c заданием vaultAddress (странно что нет у JKorf)

        }
        catch (Exception ex)
        {
            _logger.LogError($"OrdersProvider Exception {ex.Message}");
        }

        return null;
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
