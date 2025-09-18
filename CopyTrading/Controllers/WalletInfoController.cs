using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletInfoController : ControllerBase
{
    private readonly WalletInfoProvider _walletProvider;
    private readonly InformationService _informationService;

    public WalletInfoController(
        WalletInfoProvider walletProvider,
        InformationService informationService)
    {
        _walletProvider = walletProvider;
        _informationService = informationService;
    }

    [HttpGet(Name = "GetWalletInfo")]
    public async Task<ActionResult<WalletInfoModel>> Get(string wallet)
    {
        var info = await _walletProvider.GetInfo(wallet);
        if (info == null)
            return NotFound();
        return info;
    }

    [HttpGet("CalculateMinPerpEquityForCopyWallet")]
    public async Task<ActionResult<double>> CalculateMinPerpEquityForCopyWallet(string wallet)
    {
        var result = await _informationService.CalculateMinPerpEquityForHystoryTrades(wallet);
        return result;
    }

    [HttpGet("QueryPortfolio")]
    public async Task<string> QueryPortfolio(string wallet)
    {
        var result = await _walletProvider.QueryPortfolio(wallet);
        return result;
    }
}