using CopyTrading.Models.Trade;
using CopyTrading.ProviderModels.InfluxDB;
using CopyTrading.Values;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class TradeMapper
{
    public static OriginalTrade ToBll(this HyperLiquidUserTrade tradeDto, Wallet wallet)
    {
        var trade = new OriginalTrade
        {
            IsTaker = tradeDto.Crossed,
            Direction = tradeDto.OrderSide.ToBll(),
            Symbol = tradeDto.ExchangeSymbol,
            OrderId = tradeDto.OrderId,
            Price = tradeDto.Price,
            Quantity = tradeDto.Quantity,
            StartPosition = tradeDto.StartPosition,
            IsFuture = tradeDto.SymbolType == SymbolType.Futures,
            TimeStamp = tradeDto.Timestamp,
            TradeId = tradeDto.TradeId,
            Wallet = wallet,
        };

        return trade;
    }

    public static TradeMeasurement ToMeasurement(this OriginalTrade trade)
    {
        return new TradeMeasurement
        {
            Id = trade.TradeId,
            OrderId = trade.OrderId,
            Wallet = trade.Wallet.Value,
            Symbol = trade.Symbol,
            Size = trade.Quantity,
            Price = trade.Price,
            Value = trade.VolumeUsd,
            Direction = trade.Direction.ToString(),
            Time = trade.TimeStamp,
            IsFutures = trade.IsFuture,
        };
    }
}