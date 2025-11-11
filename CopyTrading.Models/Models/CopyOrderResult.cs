using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;

namespace CopyTrading.Models.Models;

/// <summary>
/// Результат попытки копирования ордера
/// </summary>
public class CopyOrderResult
{
    /// <summary>
    /// ID оригинального ордера, который пытались скопировать
    /// </summary>
    public string OriginalOrderId { get; set; }

    /// <summary>
    /// Кошелек трейдера
    /// </summary>
    public Wallet TraderWallet { get; set; }

    /// <summary>
    /// Символ (например, "BTC", "ETH")
    /// </summary>
    public string Symbol { get; set; }

    /// <summary>
    /// Статус результата копирования
    /// </summary>
    public CopyOrderResultStatus Status { get; set; }

    /// <summary>
    /// Сообщение: "Success" или описание ошибки/предупреждения
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Время создания результата
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// ID созданного копируемого ордера (если успешно)
    /// </summary>
    public string? CopyOrderId { get; set; }

    /// <summary>
    /// Успешно ли скопирован ордер (для обратной совместимости)
    /// </summary>
    public bool IsSuccess => Status == CopyOrderResultStatus.Success;

    public override string ToString()
    {
        var statusIcon = Status switch
        {
            CopyOrderResultStatus.Success => "✓",
            CopyOrderResultStatus.Warning => "⚠",
            CopyOrderResultStatus.Error => "✗",
            _ => "?"
        };

        return $"[{Timestamp:HH:mm:ss}] Order {OriginalOrderId} ({Symbol}): " +
               $"{statusIcon} {Status} - {Message}";
    }
}
