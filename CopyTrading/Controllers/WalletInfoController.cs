using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CopyTrading.Values;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletInfoController : ControllerBase
{
    private readonly IWalletInfoProvider _walletProvider;
    private readonly InformationService _informationService;

    public WalletInfoController(
        IWalletInfoProvider walletProvider,
        InformationService informationService)
    {
        _walletProvider = walletProvider;
        _informationService = informationService;
    }

    [HttpGet(Name = "GetWalletInfo")]
    public async Task<ActionResult<WalletInfoModel>> Get(string wallet)
    {
        var info = await _walletProvider.GetInfo(new Wallet(wallet));
        if (info == null)
            return NotFound();
        return info;
    }

    [HttpGet("CalculateMinPerpEquityForCopyWallet")]
    public async Task<ActionResult<decimal>> CalculateMinPerpEquityForCopyWallet(string wallet)
    {
        var result = await _informationService.CalculateMinPerpEquityForHystoryTrades(new Wallet(wallet));
        return result;
    }

    [HttpGet("QueryPortfolio")]
    public async Task<string> QueryPortfolio(string wallet)
    {
        var result = await _walletProvider.QueryPortfolio(new Wallet(wallet));
        return result;
    }
}