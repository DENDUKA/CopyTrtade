using CopyTrading.Services;
using CopyTrading.Values;
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
        _tradeService.CollectHystoricalTrades(new Wallet(wallet));
    }

    [HttpGet("SubscribeToWallet")]
    public async Task SubscribeToWallet([FromQuery] string wallet)
    {
        _tradeService.SubscribeToWalletTrades(new Wallet(wallet));
    }
}