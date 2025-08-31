using HyperLiquid.Net.Clients;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Options;

namespace CopyTrading.Providers.Hyperliquid.Providers;

public class OrdersProvider 
{
    private readonly HyperLiquidRestClient _hyperLiquidRestClient;

    private readonly string key = "0x14cd6fa02f228283077e78994d446d3a0723db46";
    private readonly string secret = "0x3ec006db11e45280c39147f6b39faf5f5a1aa8fa59ff2ab5182e1f28661f6f3a";

    public OrdersProvider()
    {
        _hyperLiquidRestClient = new HyperLiquidRestClient(new Action<HyperLiquidRestOptions>(options =>
        {
            options.ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(key, secret);
        }));
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
                Console.WriteLine($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NewOrder Exception {ex.Message}");
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
                Console.WriteLine($"NewOrder Error {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NewOrder Exception {ex.Message}");
        }

        return false;
    }

}
