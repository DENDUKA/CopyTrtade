using CopyTrading.Providers.Hyperliquid.Providers;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class WalletInfoController(WalletInfoProvider walletProvider)
{
    private readonly WalletInfoProvider _walletProvider = walletProvider;

    [HttpGet(Name = "GetWalletInfo")]
    public Task<WalletInfoModel> Get(string wallet)
    {
        return _walletProvider.GetInfo(wallet);
    }
}