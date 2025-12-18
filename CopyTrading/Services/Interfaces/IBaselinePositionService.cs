using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;

namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис для хранения базовых (начальных) позиций трейдера, которые мы не копируем.
/// Отслеживает "точку входа" для каждой пары (wallet, symbol).
/// Quantity: положительное значение = Long, отрицательное = Short.
/// </summary>
public interface IBaselinePositionService
{
    /// <summary>
    /// Инициализирует базовые позиции для всех отслеживаемых кошельков и символов
    /// </summary>
    Task Start();

    /// <summary>
    /// Обновляет базовые позиции при получении новых трейдов.
    /// При уменьшении объема текущей позиции трейдера обновляет базовую позицию.
    /// </summary>
    /// <param name="newTrades">Новые трейды</param>
    Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades);

    /// <summary>
    /// Получает базовую позицию для указанной пары (wallet, symbol).
    /// Возвращаемое значение: положительное = Long, отрицательное = Short, 0 = позиция отсутствует.
    /// </summary>
    /// <param name="wallet">Кошелек</param>
    /// <param name="symbol">Символ</param>
    /// <returns>Базовая позиция (Quantity с учетом знака) или 0 если не найдена</returns>
    Task<decimal> GetBaselinePosition(Wallet wallet, string symbol);

    /// <summary>
    /// Проверяет, закроет ли указанный ордер позицию ниже базовой линии.
    /// Возвращает результат проверки с указанием типа пересечения baseline.
    /// </summary>
    /// <param name="orderId">ID ордера для проверки</param>
    /// <returns>Результат проверки: AboveBaseline, CrossesBaseline или AlreadyBelowBaseline</returns>
    Task<BaselineCheckResult> WillOrderCloseBelowBaseline(long orderId);
}
