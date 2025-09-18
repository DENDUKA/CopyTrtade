using CopyTrading.Models;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class WalletInfoMapper
{
    public static WalletInfoModel ToBll(this HyperLiquidFuturesAccount data, string wallet)
    {
        return new WalletInfoModel()
        {
            Wallet = wallet,
            AccountVolume = (double)data.MarginSummary.AccountValue,
            TotalMarginUsed = data.MarginSummary.TotalMarginUsed,
            Positions = data.Positions
            .Select(x => x.ToBll())
            .ToDictionary(k => k.Symbol, v => v),
        };
    }

    public static PositionModel ToBll(this HyperLiquidPosition data)
    {
        return new PositionModel()
        {
            Symbol = data.Position.Symbol,
            AverageEntryPrice = (double?)data.Position.AverageEntryPrice,
            Leverage = data.Position.Leverage?.Value,
            LiquidationPrice = (double?)data.Position.LiquidationPrice,
            MarginUsage = (double?)data.Position.MarginUsed,
            Quantity = (double?)data.Position.PositionQuantity,
            ValueUsd = (double?)data.Position.PositionValue,
        };
    }
}
