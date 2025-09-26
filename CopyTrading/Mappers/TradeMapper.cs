using CopyTrading.Models.Trade;
using CopyTrading.ProviderModels.InfluxDB;
using HyperLiquid.Net.Enums;
using HyperLiquid.Net.Objects.Models;

namespace CopyTrading.Mappers;

public static class TradeMapper
{
    public static TradeModel ToBll(this HyperLiquidUserTrade tradeDto, string wallet)
    {
        var trade = new TradeModel
        {
            IsTaker = tradeDto.Crossed,
            Direction = tradeDto.OrderSide.ToBll(),
            Coin = tradeDto.ExchangeSymbol,
            OrderId = tradeDto.OrderId,
            Price = (double)tradeDto.Price,
            Quantity = (double)tradeDto.Quantity,
            StartPosition = (double)tradeDto.StartPosition,
            IsFuture = tradeDto.SymbolType == SymbolType.Futures,
            TimeStamp = tradeDto.Timestamp,
            TradeId = tradeDto.TradeId,
            Wallet = wallet,
        };

        return trade;
    }

    public static TradeMeasurement ToMeasurement(this TradeModel trade)
    {
        return new TradeMeasurement
        {
            Id = trade.TradeId,
            OrderId = trade.OrderId,
            Wallet = trade.Wallet,
            Coin = trade.Coin,
            Size = trade.Quantity,
            Price = trade.Price,
            Value = trade.Volume,
            Direction = trade.Direction.ToString(),
            Time = trade.TimeStamp,
            IsFutures = trade.IsFuture,
        };
    }
}