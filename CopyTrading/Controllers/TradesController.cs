using CopyTrading.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class TradesController(
    TradeService _tradeService) : ControllerBase
{
    [HttpGet("SubscribeToTrackedWallets")]
    public async Task SubscribeToTrackedWallets()
    {
        _tradeService.SubscribeToTrackedWalletsTrades();
    }

    [HttpGet("CollectWalletHistoryTrades")]
    public async Task CollectWalletHistoryTrades([FromQuery] string wallet)
    {
        _tradeService.CollectHystoricalTrades(wallet);
    }

    [HttpGet("SubscribeToWallet")]
    public async Task SubscribeToWallet([FromQuery] string wallet)
    {
        _tradeService.SubscribeToWalletTrades(wallet);
    }
}