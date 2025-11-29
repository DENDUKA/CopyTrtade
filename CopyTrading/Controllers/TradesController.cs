using CopyTrading.Models.Values;
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
        await _tradeService.SubscribeToTrackedWalletsTrades();
    }


    [HttpGet("SubscribeToWallet")]
    public async Task SubscribeToWallet([FromQuery] string wallet)
    {
        await _tradeService.SubscribeToWalletTrades(new Wallet(wallet));
    }
}