using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Repository.SQLite;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

public class InformationService(
    IWalletInfoProvider _walletInfoProvider,
    TradeRepository _tradeRepository)
{
    public async Task<decimal> CalculateMinPerpEquityForHystoryTrades(Wallet wallet)
    {
        var trades = await _walletInfoProvider.GetHistoricalTrades(wallet);
        var walletInfo = await _walletInfoProvider.GetInfo(wallet);

        var minPerpE = decimal.MinValue;
        //Тут надо получать Value Wallet в определенный момент времени ( трейда ) и вычислять исходя из него
        foreach (var t in trades.Take(100))
        {
            var tradeEquity = 100 / (t.VolumeUsd / walletInfo.AccountVolume * 100) * 10;
            if (tradeEquity > minPerpE)
            {
                minPerpE = tradeEquity;
            }
        }

        return minPerpE;
    }

    public async Task LogMinPerpEquityForTrade(OriginalTrade trade, decimal spread, decimal deltaTimeS)
    {
        var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet);

        var minPE = CalculateMinPerpEquity(walletInfo.AccountVolume, trade.VolumeUsd);

        _tradeRepository.WriteMinPeForTrade(new MinPEForTrade
        {
            TradeId = trade.TradeId,
            AccountVolume = walletInfo.AccountVolume,
            DeltaTimeS = deltaTimeS,
            MinPE = minPE,
            Spread = spread,            
            SubType = trade.SubType,
        });
    }

    public decimal CalculateMinPerpEquity(decimal walletVolume, decimal tradeVolume)
    {
        try
        {
            return 100 / (tradeVolume / walletVolume * 100);
        }
        catch (Exception ex)
        {
            return 0;
        }
    }
}