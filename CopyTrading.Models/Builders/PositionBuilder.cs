using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;

namespace CopyTrading.Models.Builders;

public class PositionBuilder
{
    private string _symbol;
    private decimal _quantity;
    private decimal _price;
    private int _leverage = 5;

    public PositionBuilder WithSymbol(string symbol)
    {
        _symbol = symbol;
        return this;
    }

    public PositionBuilder WithQuantity(decimal quantity)
    {
        _quantity = quantity;
        return this;
    }

    public PositionBuilder WithAverageEntryPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public PositionBuilder WithLeverage(int leverage)
    {
        _leverage = leverage;
        return this;
    }

    public Dictionary<string, Position> BuildDictionary()
    {
        return new()
        {
            {
                _symbol,
                new Position
                {
                    Symbol = _symbol,
                    Quantity = _quantity,
                    AverageEntryPrice = _price,
                    Leverage = _leverage,
                }
            }
        };
    }
}
