using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController(
    OrderService orderService,
    OrdersProvider ordersHyperliquidProvider,
    IWalletInfoProvider walletInfoJKirfProvider,
    IExchangeInfoProvider exchangeInfoProvider) : ControllerBase
{
    private readonly OrderService _orderService = orderService;
    private readonly OrdersProvider _ordersHyperliquidProvider = ordersHyperliquidProvider;
    private readonly IWalletInfoProvider _walletInfoJKirfProvider = walletInfoJKirfProvider;
    private readonly IExchangeInfoProvider _exchangeInfoProvider = exchangeInfoProvider;

    [HttpGet("GetOrdersHistory")]
    public async Task<ActionResult<OriginalOrder[]>> GetOrdersHistory([FromQuery] string wallet)
    {
        //var result = await _ordersHyperliquidProvider.GetHistoricalOrdersForWallet(wallet);
        return Ok();
    }

    [HttpGet("CollectOrdersHistoryForAllWallets")]
    public async Task CollectOrdersHistoryForAllWallets()
    {
        _orderService.CollectHistoryOrders();
        
    }

    [HttpGet("SubscribeToNewOrders")]
    public async Task<IActionResult> SubscribeToNewOrders([FromQuery] string wallet)
    {
        _orderService.SubscribeToWalletOrders(new Wallet(wallet));
        return Ok();
    }

    [HttpGet("SubscribeToTrackedWallets")]
    public async Task<IActionResult> SubscribeToTrackedWallets()
    {
        _orderService.SubscribeToTrackedWalletsOrders();
        return Ok();
    }

    [HttpGet("SetOrderTest")]
    public async Task SetOrderTest()
    {
        await _ordersHyperliquidProvider.PlaceOrder();
    }

    [HttpGet("GetWalletInfo")]
    public async Task GetWalletInfo([FromQuery] string wallet)
    {
        var info =  await _walletInfoJKirfProvider.GetInfo(new Wallet(wallet));
    }

    [HttpGet("ExchangeInfoProvider")]
    public async Task ExchangeInfoProvider()
    {
        await _exchangeInfoProvider.GetExchangeInfo();
    }

    [HttpGet("TestExchangeInfo")]
    public async Task TestExchangeInfo([FromQuery] string symbol)
    {
        var res =  await _exchangeInfoProvider.GetExchangeInfo(symbol);
    }
}