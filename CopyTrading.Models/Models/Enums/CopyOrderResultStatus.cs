namespace CopyTrading.Models.Models.Enums;

/// <summary>
/// Статус результата копирования ордера
/// </summary>
public enum CopyOrderResultStatus
{
    /// <summary>
    /// Ордер успешно скопирован
    /// </summary>
    Success,

    /// <summary>
    /// Предупреждение (например, ордер не скопирован из-за настроек, но это не ошибка)
    /// </summary>
    Warning,

    /// <summary>
    /// Ошибка при копировании ордера
    /// </summary>
    Error
}
