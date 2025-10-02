namespace CopyTrading.Models.Orders;

public class CopyOrder : Order
{

    /// <summary>
    /// размер ордера / баланс кошелька (копируемого) в %
    /// </summary>
    public decimal OrderRatio { get; set; }

    public OriginalOrder OriginalOrder { get; set; }

    public override string ToString()
    {
        return $"Открытие ордера {OrderId} монета {Symbol} на сумму {Price * Quantity} кол-во монет {Quantity} по цене {Price} level {Leverage}" +
            $" маржа : {Price * Quantity / Leverage} Позиция в % от капитала : {OrderRatio}";
    }
}