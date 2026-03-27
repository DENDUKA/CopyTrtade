using CopyTrading.Providers.Hyperliquid.Subscribers;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthCheckController(
    IOrdersTradesSubscriber _subscribers) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            timestampUtc = DateTime.UtcNow
        });
    }

    [HttpGet("SubscribersStatus")]
    public async Task<string[]> SubscribersStatus()
    {
        return
        [
            .. _subscribers.OrdersSubscriptionStatus
                            .Select(x => $"order {x.Item1} {x.Item2}")
,
            .. _subscribers.TradesSubscriptionStatus
                    .Select(x => $"task {x.Item1} {x.Item2}"),
        ];
    }
}
