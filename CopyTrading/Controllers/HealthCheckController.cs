using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthCheckController(
    FillsOrderService _fillsOrderService,
    OrdersTradesSubscriber _subscribers) : ControllerBase
{
    [HttpGet("GetErrorsInFillsOrderService")]
    public async Task<string[]> GetErrorsInFillsOrderService()
    {
        return _fillsOrderService._ordersWithError.Select(x => $"{x.Key} {x.Value}").ToArray();
    }

    [HttpGet("SubscribersStatus")]
    public (Wallet, bool)[] SubscribersStatus()
    {
        return _subscribers.OrdersSubscriptionStatus.Concat(_subscribers.TradesSubscriptionStatus).ToArray();
    }
}
