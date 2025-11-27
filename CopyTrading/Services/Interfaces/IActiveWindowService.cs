using CopyTrading.Models.Models.Enums.Order;
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
    /// Получает ближайшие ордера для указанного кошелька и символа
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <param name="count">Количество ордеров для каждого направления (по умолчанию 3)</param>
    /// <returns>Кортеж с массивами Long и Short ордеров</returns>
    (OrderFills[] LongOrders, OrderFills[] ShortOrders) GetNearestOrders(Wallet wallet, string symbol, int count = 3);
}
