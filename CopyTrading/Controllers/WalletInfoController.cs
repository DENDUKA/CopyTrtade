using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletInfoController : ControllerBase
{
    private readonly IWalletInfoProvider _walletProvider;

    public WalletInfoController(
        IWalletInfoProvider walletProvider)
    {
        _walletProvider = walletProvider;
    }

    [HttpGet(Name = "GetWalletInfo")]
    public async Task<ActionResult<WalletInfoModel>> Get(string wallet)
    {
        var info = await _walletProvider.GetInfo(new Wallet(wallet));
        if (info == null)
            return NotFound();
        return info;
    }

    [HttpGet("QueryPortfolio")]
    public async Task<string> QueryPortfolio(string wallet)
    {
        var result = await _walletProvider.QueryPortfolio(new Wallet(wallet));
        return result;
    }
}