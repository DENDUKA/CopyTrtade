using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletInfoController(
    IWalletInfoProvider walletProvider) : ControllerBase
{
    [HttpGet(Name = "GetWalletInfo")]
    public async Task<ActionResult<WalletInfoModel>> Get(string wallet)
    {
        var info = await walletProvider.GetInfo(new Wallet(wallet));
        if (info == null)
            return NotFound();
        return info;
    }

    [HttpGet("QueryPortfolio")]
    public async Task<string> QueryPortfolio(string wallet)
    {
        var result = await walletProvider.QueryPortfolio(new Wallet(wallet));
        return result;
    }
}