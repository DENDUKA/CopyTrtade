using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models;

/// <summary>
/// Маппинг между позицией трейдера и вашей копируемой позицией
/// Используется для отслеживания пропорции при увеличении/уменьшении позиций
/// </summary>
public class PositionMapping
{
    /// <summary>
    /// Кошелек трейдера (который копируем)
    /// </summary>
    public Wallet TraderWallet { get; set; }

    /// <summary>
    /// Ваш кошелек
    /// </summary>
    public Wallet MyWallet { get; set; }

    /// <summary>
    /// Символ (например, "BTC", "ETH")
    /// </summary>
    public string Symbol { get; set; }

    /// <summary>
    /// Направление позиции (Long/Short)
    /// </summary>
    public Direction Direction { get; set; }

    /// <summary>
    /// Текущее количество в вашей позиции
    /// </summary>
    public decimal MyQuantity { get; set; }

    /// <summary>
    /// Коэффициент между позициями (MyQuantity / TraderQuantity при открытии)
    /// Сохраняется при первом открытии позиции и используется для всех последующих операций.
    /// Позиция трейдера всегда получается из CurrentWalletPositionService.GetSnapshot()
    /// </summary>
    public decimal PositionRatio { get; set; }

    /// <summary>
    /// Время последнего обновления
    /// </summary>
    public DateTime LastUpdate { get; set; }

    public override string ToString()
    {
        return $"{TraderWallet} -> {MyWallet} | {Symbol} {Direction} | " +
               $"My: {MyQuantity}, Ratio: {PositionRatio:F6}";
    }

    /// <summary>
    /// Создает ключ для уникальной идентификации маппинга
    /// </summary>
    public string GetKey()
    {
        return $"{TraderWallet}_{MyWallet}_{Symbol}_{Direction}";
    }

    /// <summary>
    /// Создает ключ для маппинга по параметрам
    /// </summary>
    public static string CreateKey(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        return $"{traderWallet}_{myWallet}_{symbol}_{direction}";
    }
}
