namespace CopyTrading.Models.Orders;

public class CopyOrder : Order
{

    /// <summary>
    /// размер ордера / баланс кошелька (копируемого) в %
    /// </summary>
    public double OrderRatio { get; set; }

    public OriginalOrder OriginalOrder { get; set; }

    public override string ToString()
    {
        return $"Открытие ордера {Id} монета {Coin} на сумму {Price * Size} кол-во монет {Size} по цене {Price} level {Leverage}" +
            $" маржа : {Price * Size / Leverage} Позиция в % от капитала : {OrderRatio}";
    }
}