using CopyTrading.Services;
using Microsoft.AspNetCore.Mvc;

namespace CopyTrading.Controllers;

[ApiController]
[Route("[controller]")]
public class CandlesController(
    CandleService _candleService) : ControllerBase
{
    [HttpGet("CollectCandlesForSymbol")]
    public async Task CollectCandlesForSymbol([FromQuery] string symbol)
    {
        _candleService.CollectCandles(symbol);
    }
}