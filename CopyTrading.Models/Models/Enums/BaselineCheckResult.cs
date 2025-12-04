namespace CopyTrading.Models.Models.Enums;

/// <summary>
/// Результат проверки ордера относительно базовой линии
/// </summary>
public enum BaselineCheckResult
{
    /// <summary>
    /// Наш и предшествующие ордера НЕ заходят в baseline (можно копировать полностью)
    /// </summary>
    AboveBaseline,

    /// <summary>
    /// Именно наш ордер пересекает baseline (может потребоваться частичное копирование)
    /// </summary>
    CrossesBaseline,

    /// <summary>
    /// Baseline была пересечена ещё до нашего ордера (не копировать)
    /// </summary>
    AlreadyBelowBaseline
}
