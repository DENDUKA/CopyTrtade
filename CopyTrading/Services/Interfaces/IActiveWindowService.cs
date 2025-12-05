using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис для реализации стратегии "Just-in-Time Provisioning" / "Active Window Strategy"
/// Находит ближайшие ордера для копирования по цене
/// </summary>
public interface IActiveWindowService
{
    /// <summary>
    /// Получает ближайшие ордера для указанного кошелька, символа и направления.
    /// Long ордера: сортируются по цене от высокой к низкой (ближайшие к исполнению при падении цены)
    /// Short ордера: сортируются по цене от низкой к высокой (ближайшие к исполнению при росте цены)
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <param name="direction">Направление ордеров (Long или Short)</param>
    /// <param name="count">Количество ордеров (по умолчанию 3)</param>
    /// <returns>Массив ближайших ордеров указанного направления</returns>
    OriginalOrder[] GetNearestOrders(Wallet wallet, string symbol, Direction direction, int count = 3);
}
