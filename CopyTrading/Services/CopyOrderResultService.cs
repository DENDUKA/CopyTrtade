using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для отслеживания результатов копирования ордеров
/// Хранит информацию об успешных и неудачных попытках копирования
/// Хранит данные в Redis для персистентности
/// </summary>
public class CopyOrderResultService : ICopyOrderResultService
{
    private readonly IRedisRepository _redisRepository;
    private readonly ILogger<CopyOrderResultService> _logger;

    // Константы для управления размером кэша
    private const int MaxResultsThreshold = 2000;  // Порог для начала очистки
    private const int TargetResultsCount = 500;    // Целевое количество после очистки

    public CopyOrderResultService(
        IRedisRepository redisRepository,
        ILogger<CopyOrderResultService> logger)
    {
        _redisRepository = redisRepository;
        _logger = logger;
    }

    /// <summary>
    /// Сохраняет результат успешного копирования ордера
    /// </summary>
    public void SaveSuccess(string originalOrderId, Wallet traderWallet, string symbol, string copyOrderId)
    {
        try
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

            _redisRepository.SaveCopyOrderResult(result).GetAwaiter().GetResult();
            _logger.LogInformation($"CopyOrderResult SUCCESS: {result}");

            // Проверяем необходимость очистки после добавления результата
            CleanupOldResultsIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save success result to Redis");
        }
    }

    /// <summary>
    /// Сохраняет результат неудачного копирования ордера (ошибка)
    /// </summary>
    public void SaveFailure(string originalOrderId, Wallet traderWallet, string symbol, string errorMessage)
    {
        try
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

            _redisRepository.SaveCopyOrderResult(result).GetAwaiter().GetResult();
            _logger.LogError($"CopyOrderResult ERROR: {result}");

            // Проверяем необходимость очистки после добавления результата
            CleanupOldResultsIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save failure result to Redis");
        }
    }

    /// <summary>
    /// Сохраняет предупреждение (ордер не скопирован, но это не ошибка)
    /// </summary>
    public void SaveWarning(string originalOrderId, Wallet traderWallet, string symbol, string warningMessage)
    {
        try
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

            _redisRepository.SaveCopyOrderResult(result).GetAwaiter().GetResult();
            _logger.LogWarning($"CopyOrderResult WARNING: {result}");

            // Проверяем необходимость очистки после добавления результата
            CleanupOldResultsIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save warning result to Redis");
        }
    }

    /// <summary>
    /// Получить результат по ID оригинального ордера
    /// </summary>
    public CopyOrderResult? GetResult(string originalOrderId)
    {
        try
        {
            return _redisRepository.GetCopyOrderResult(originalOrderId).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy order result from Redis");
            return null;
        }
    }

    /// <summary>
    /// Получить все результаты
    /// </summary>
    public IEnumerable<CopyOrderResult> GetAllResults()
    {
        try
        {
            var results = _redisRepository.LoadAllCopyOrderResults().GetAwaiter().GetResult();
            return results.Values.OrderByDescending(r => r.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all copy order results from Redis");
            return Enumerable.Empty<CopyOrderResult>();
        }
    }

    /// <summary>
    /// Получить статистику по результатам
    /// </summary>
    public (int Total, int Success, int Warning, int Error, double SuccessRate) GetStatistics()
    {
        try
        {
            var results = _redisRepository.LoadAllCopyOrderResults().GetAwaiter().GetResult();
            var allResults = results.Values.ToList();
            var total = allResults.Count;
            var success = allResults.Count(r => r.Status == CopyOrderResultStatus.Success);
            var warning = allResults.Count(r => r.Status == CopyOrderResultStatus.Warning);
            var error = allResults.Count(r => r.Status == CopyOrderResultStatus.Error);
            var successRate = total > 0 ? (double)success / total * 100 : 0;

            return (total, success, warning, error, successRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy order results statistics from Redis");
            return (0, 0, 0, 0, 0.0);
        }
    }

    /// <summary>
    /// Очистить все результаты (используется для тестирования)
    /// </summary>
    public void ClearAllResults()
    {
        try
        {
            var results = _redisRepository.LoadAllCopyOrderResults().GetAwaiter().GetResult();
            var count = results.Count;

            if (count > 0)
            {
                _redisRepository.DeleteCopyOrderResults(results.Keys).GetAwaiter().GetResult();
            }

            _logger.LogInformation($"Все результаты очищены (было {count})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all copy order results from Redis");
        }
    }

    /// <summary>
    /// Автоматическая очистка старых результатов при превышении порога
    /// Удаляет старые результаты (по времени) до достижения целевого количества
    /// </summary>
    private void CleanupOldResultsIfNeeded()
    {
        try
        {
            var results = _redisRepository.LoadAllCopyOrderResults().GetAwaiter().GetResult();

            // Проверяем, превышен ли порог
            if (results.Count <= MaxResultsThreshold)
            {
                return;
            }

            _logger.LogInformation($"CopyOrderResultService: Начинаем очистку старых результатов. Текущее количество: {results.Count}");

            // Получаем все результаты, отсортированные по времени (от старых к новым)
            var resultsToRemove = results.Values
                .OrderBy(result => result.Timestamp)
                .Take(results.Count - TargetResultsCount)
                .ToList();

            _logger.LogInformation($"CopyOrderResultService: Будет удалено {resultsToRemove.Count} старых результатов");

            if (resultsToRemove.Count > 0)
            {
                // Удаляем старые результаты batch-операцией
                var resultIdsToRemove = resultsToRemove.Select(r => r.OriginalOrderId);
                _redisRepository.DeleteCopyOrderResults(resultIdsToRemove).GetAwaiter().GetResult();

                foreach (var result in resultsToRemove)
                {
                    _logger.LogDebug($"CopyOrderResultService: Удален результат OriginalOrderId={result.OriginalOrderId}, " +
                                   $"Symbol={result.Symbol}, Timestamp={result.Timestamp}, Status={result.Status}");
                }
            }

            var remainingCount = results.Count - resultsToRemove.Count;
            _logger.LogInformation($"CopyOrderResultService: Очистка завершена. Удалено: {resultsToRemove.Count}, Осталось: {remainingCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old copy order results from Redis");
        }
    }
}
