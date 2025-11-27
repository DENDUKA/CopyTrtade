using System.Collections.Concurrent;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для отслеживания результатов копирования ордеров
/// Хранит информацию об успешных и неудачных попытках копирования
/// </summary>
public class CopyOrderResultService : ICopyOrderResultService
{
    private readonly ConcurrentDictionary<string, CopyOrderResult> _results;
    private readonly ILogger<CopyOrderResultService> _logger;

    // Константы для управления размером кэша
    private const int MaxResultsThreshold = 2000;  // Порог для начала очистки
    private const int TargetResultsCount = 500;    // Целевое количество после очистки

    public CopyOrderResultService(ILogger<CopyOrderResultService> logger)
    {
        _results = new ConcurrentDictionary<string, CopyOrderResult>();
        _logger = logger;
    }

    /// <summary>
    /// Сохраняет результат успешного копирования ордера
    /// </summary>
    public void SaveSuccess(string originalOrderId, Wallet traderWallet, string symbol, string copyOrderId)
    {
        var result = new CopyOrderResult
        {
            OriginalOrderId = originalOrderId,
            TraderWallet = traderWallet,
            Symbol = symbol,
            Status = CopyOrderResultStatus.Success,
            Message = "Success",
            Timestamp = DateTime.Now,
            CopyOrderId = copyOrderId
        };

        _results[originalOrderId] = result;
        _logger.LogInformation($"CopyOrderResult SUCCESS: {result}");

        // Проверяем необходимость очистки после добавления результата
        CleanupOldResultsIfNeeded();
    }

    /// <summary>
    /// Сохраняет результат неудачного копирования ордера (ошибка)
    /// </summary>
    public void SaveFailure(string originalOrderId, Wallet traderWallet, string symbol, string errorMessage)
    {
        var result = new CopyOrderResult
        {
            OriginalOrderId = originalOrderId,
            TraderWallet = traderWallet,
            Symbol = symbol,
            Status = CopyOrderResultStatus.Error,
            Message = errorMessage,
            Timestamp = DateTime.Now,
            CopyOrderId = null
        };

        _results[originalOrderId] = result;
        _logger.LogError($"CopyOrderResult ERROR: {result}");

        // Проверяем необходимость очистки после добавления результата
        CleanupOldResultsIfNeeded();
    }

    /// <summary>
    /// Сохраняет предупреждение (ордер не скопирован, но это не ошибка)
    /// </summary>
    public void SaveWarning(string originalOrderId, Wallet traderWallet, string symbol, string warningMessage)
    {
        var result = new CopyOrderResult
        {
            OriginalOrderId = originalOrderId,
            TraderWallet = traderWallet,
            Symbol = symbol,
            Status = CopyOrderResultStatus.Warning,
            Message = warningMessage,
            Timestamp = DateTime.Now,
            CopyOrderId = null
        };

        _results[originalOrderId] = result;
        _logger.LogWarning($"CopyOrderResult WARNING: {result}");

        // Проверяем необходимость очистки после добавления результата
        CleanupOldResultsIfNeeded();
    }

    /// <summary>
    /// Получить результат по ID оригинального ордера
    /// </summary>
    public CopyOrderResult? GetResult(string originalOrderId)
    {
        _results.TryGetValue(originalOrderId, out var result);
        return result;
    }

    /// <summary>
    /// Получить все результаты
    /// </summary>
    public IEnumerable<CopyOrderResult> GetAllResults()
    {
        return _results.Values.OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Получить только успешные результаты
    /// </summary>
    public IEnumerable<CopyOrderResult> GetSuccessfulResults()
    {
        return _results.Values
            .Where(r => r.Status == CopyOrderResultStatus.Success)
            .OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Получить только результаты с предупреждениями
    /// </summary>
    public IEnumerable<CopyOrderResult> GetWarningResults()
    {
        return _results.Values
            .Where(r => r.Status == CopyOrderResultStatus.Warning)
            .OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Получить только неудачные результаты (ошибки)
    /// </summary>
    public IEnumerable<CopyOrderResult> GetFailedResults()
    {
        return _results.Values
            .Where(r => r.Status == CopyOrderResultStatus.Error)
            .OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Получить результаты за определенный период
    /// </summary>
    public IEnumerable<CopyOrderResult> GetResultsSince(DateTime since)
    {
        return _results.Values
            .Where(r => r.Timestamp >= since)
            .OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Очистить старые результаты (старше указанного времени)
    /// </summary>
    public int ClearOldResults(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.Now - olderThan;
        var oldKeys = _results
            .Where(kvp => kvp.Value.Timestamp < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        int removedCount = 0;
        foreach (var key in oldKeys)
        {
            if (_results.TryRemove(key, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation($"Удалено {removedCount} старых результатов (старше {olderThan})");
        }

        return removedCount;
    }

    /// <summary>
    /// Получить статистику по результатам
    /// </summary>
    public (int Total, int Success, int Warning, int Error, double SuccessRate) GetStatistics()
    {
        var allResults = _results.Values.ToList();
        var total = allResults.Count;
        var success = allResults.Count(r => r.Status == CopyOrderResultStatus.Success);
        var warning = allResults.Count(r => r.Status == CopyOrderResultStatus.Warning);
        var error = allResults.Count(r => r.Status == CopyOrderResultStatus.Error);
        var successRate = total > 0 ? (double)success / total * 100 : 0;

        return (total, success, warning, error, successRate);
    }

    /// <summary>
    /// Очистить все результаты (используется для тестирования)
    /// </summary>
    public void ClearAllResults()
    {
        var count = _results.Count;
        _results.Clear();
        _logger.LogInformation($"Все результаты очищены (было {count})");
    }

    /// <summary>
    /// Автоматическая очистка старых результатов при превышении порога
    /// Удаляет старые результаты (по времени) до достижения целевого количества
    /// </summary>
    private void CleanupOldResultsIfNeeded()
    {
        // Проверяем, превышен ли порог
        if (_results.Count <= MaxResultsThreshold)
        {
            return;
        }

        _logger.LogInformation($"CopyOrderResultService: Начинаем очистку старых результатов. Текущее количество: {_results.Count}");

        // Получаем все результаты, отсортированные по времени (от старых к новым)
        var resultsToRemove = _results.Values
            .OrderBy(result => result.Timestamp)
            .Take(_results.Count - TargetResultsCount)
            .ToList();

        _logger.LogInformation($"CopyOrderResultService: Будет удалено {resultsToRemove.Count} старых результатов");

        int removedCount = 0;

        // Удаляем старые результаты
        foreach (var result in resultsToRemove)
        {
            if (_results.TryRemove(result.OriginalOrderId, out _))
            {
                removedCount++;
                _logger.LogDebug($"CopyOrderResultService: Удален результат OriginalOrderId={result.OriginalOrderId}, " +
                               $"Symbol={result.Symbol}, Timestamp={result.Timestamp}, Status={result.Status}");
            }
        }

        _logger.LogInformation($"CopyOrderResultService: Очистка завершена. Удалено: {removedCount}, Осталось: {_results.Count}");
    }
}
