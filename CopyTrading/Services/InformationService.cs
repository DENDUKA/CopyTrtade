using CopyTrading.Models.Trade;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Repository.SQLite;
using CopyTrading.Repository.SQLite.Dto;

namespace CopyTrading.Services;

public class InformationService(
    WalletInfoProvider _walletInfoProvider,
    TradeRepository _tradeRepository)
{
    public async Task<double> CalculateMinPerpEquityForHystoryTrades(string wallet)
    {
        var trades = await _walletInfoProvider.GetHistoricalTrades(wallet);
        var walletInfo = await _walletInfoProvider.GetInfo(wallet);

        var minPerpE = double.MinValue;
        //Тут надо получать Value Wallet в определенный момент времени ( трейда ) и вычислять исходя из него
        foreach (var t in trades.Take(100))
        {
            var tradeEquity = 100 / (t.Volume / walletInfo.AccountVolume * 100) * 10;
            if (tradeEquity > minPerpE)
            {
                minPerpE = tradeEquity;
            }
        }

        return minPerpE;
    }

    public async Task LogMinPerpEuqityForTrade(OriginalTrade trade, double spread, double deltaTimeS)
    {
        var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet);

        var minPE = CalculateMinPerpEquity(walletInfo.AccountVolume, trade.Volume);

        _tradeRepository.WriteMinPeForTrade(new MinPEForTradeDto
        {
            AccountVolume = walletInfo.AccountVolume,
            DeltaTimeS = deltaTimeS,
            MinPE = minPE,
            Spread = spread,
            Symbol = trade.Coin,
            Time = trade.TimeStamp,
            TradeId = trade.TradeId,
            Volume = trade.Volume,
            Wallet = trade.Wallet,
            OrderId = trade.OrderId,
        });
    }

    private double CalculateMinPerpEquity(double walletVolume, double tradeVolume)
    {
        return 100 / (tradeVolume / walletVolume * 100) * 10;
    }
}