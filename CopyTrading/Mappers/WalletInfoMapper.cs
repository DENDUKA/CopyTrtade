using CopyTrading.Models.Models;
using CopyTrading.Models.Values;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class WalletInfoMapper
{
    public static WalletInfoModel ToBll(this HyperLiquidFuturesAccount data, Wallet wallet)
    {
        return new WalletInfoModel()
        {
            Wallet = wallet,
            AccountVolume = data.MarginSummary.AccountValue,
            TotalMarginUsed = data.MarginSummary.TotalMarginUsed,
            TimeStamp = data.Timestamp,
            Positions = data.Positions
            .Select(x => x.ToBll())
            .ToDictionary(k => k.Symbol, v => v),
        };
    }

    public static Position ToBll(this HyperLiquidPosition data)
    {
        return new Position()
        {
            Symbol = data.Position.Symbol,
            AverageEntryPrice = data.Position.AverageEntryPrice.Value,
            Leverage = data.Position.Leverage.Value,
            LiquidationPrice = data.Position.LiquidationPrice,
            MarginUsage = data.Position.MarginUsed.Value,
            Quantity = data.Position.PositionQuantity.Value,
            VolumeUsd = data.Position.PositionValue.Value,
        };
    }

    public static WalletPositionsSnapshot ToWalletSnapshot(this WalletInfoModel info)
    {
        return new WalletPositionsSnapshot()
        {
            Wallet = info.Wallet,
            TimeStamp = info.TimeStamp,
            Positions = info.Positions.Values.ToList(),
        };
    }
}
