using System.Collections.Concurrent;
using CopyTrading.Models.Models;
using CopyTrading.Models.Values;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для отслеживания результатов копирования ордеров
/// Хранит информацию об успешных и неудачных попытках копирования
/// </summary>
public class CopyOrderResultService
{
    private readonly ConcurrentDictionary<string, CopyOrderResult> _results;
    private readonly ILogger<CopyOrderResultService> _logger;

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
            IsSuccess = true,
            Message = "Success",
            Timestamp = DateTime.UtcNow,
            CopyOrderId = copyOrderId
        };

        _results[originalOrderId] = result;
        _logger.LogInformation($"CopyOrderResult SUCCESS: {result}");
    }

    /// <summary>
    /// Сохраняет результат неудачного копирования ордера
    /// </summary>
    public void SaveFailure(string originalOrderId, Wallet traderWallet, string symbol, string errorMessage)
    {
        var result = new CopyOrderResult
        {
            OriginalOrderId = originalOrderId,
            TraderWallet = traderWallet,
            Symbol = symbol,
            IsSuccess = false,
            Message = errorMessage,
            Timestamp = DateTime.UtcNow,
            CopyOrderId = null
        };

        _results[originalOrderId] = result;
        _logger.LogWarning($"CopyOrderResult FAILURE: {result}");
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
            .Where(r => r.IsSuccess)
            .OrderByDescending(r => r.Timestamp);
    }

    /// <summary>
    /// Получить только неудачные результаты
    /// </summary>
    public IEnumerable<CopyOrderResult> GetFailedResults()
    {
        return _results.Values
            .Where(r => !r.IsSuccess)
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
        var cutoffTime = DateTime.UtcNow - olderThan;
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
    public (int Total, int Success, int Failed, double SuccessRate) GetStatistics()
    {
        var allResults = _results.Values.ToList();
        var total = allResults.Count;
        var success = allResults.Count(r => r.IsSuccess);
        var failed = total - success;
        var successRate = total > 0 ? (double)success / total * 100 : 0;

        return (total, success, failed, successRate);
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
}
