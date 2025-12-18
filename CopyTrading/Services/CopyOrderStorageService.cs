using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;
using System.Threading.Tasks;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для хранения и управления копируемыми ордерами
/// Отслеживает все ордера, которые мы создаем на основе ордеров трейдеров
/// Хранит данные в Redis для персистентности
/// </summary>
public class CopyOrderStorageService : ICopyOrderStorageService
{
    private readonly IRedisRepository _redisRepository;
    private readonly ILogger<CopyOrderStorageService> _logger;

    // Константы для управления размером кэша
    private const int MaxOrdersThreshold = 2000;  // Порог для начала очистки
    private const int TargetOrdersCount = 500;    // Целевое количество после очистки

    public CopyOrderStorageService(
        IRedisRepository redisRepository,
        ILogger<CopyOrderStorageService> logger)
    {
        _redisRepository = redisRepository;
        _logger = logger;

        // Подписываемся на события создания и закрытия копируемых ордеров
        DataBusEvents.CopyOrderCreated += OnCopyOrderCreated;
    }

    /// <summary>
    /// Обработчик события создания копируемого ордера
    /// </summary>
    private void OnCopyOrderCreated(CopyOrderV2 copyOrder)
    {
        _logger.LogInformation($"CopyOrderStorageService OnCopyOrderCreated {copyOrder.ToString()}");
        AddOrder(copyOrder);
    }

    /// <summary>
    /// Добавить копируемый ордер в хранилище
    /// </summary>
    public async Task<bool> AddOrder(CopyOrderV2 order)
    {
        try
        {
            // Сохраняем в Redis
            await _redisRepository.SaveCopyOrder(order);

            _logger.LogInformation(
                $"Копируемый ордер добавлен: ID={order.OrderId}, Symbol={order.OriginalOrder.Symbol}, " +
                $"Direction={order.OriginalOrder.Direction}, Price={order.OriginalOrder.Price}, Quantity={order.Quantity}, " +
                $"OriginalOrderId={order.OriginalOrderId}");

            // Проверяем необходимость очистки после добавления ордера
            await CleanupOldOrdersIfNeeded();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при добавлении копируемого ордера ID={order.OrderId}");
            return false;
        }
    }

    /// <summary>
    /// Получить все копируемые ордера
    /// </summary>
    public async Task<CopyOrderV2[]> GetAllOrders()
    {
        try
        {
            var orders = await _redisRepository.LoadAllCopyOrders();
            return [.. orders.Values];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all copy orders from Redis");
            return [];
        }
    }

    /// <summary>
    /// Получить копируемые ордера по статусу
    /// </summary>
    public async Task<CopyOrderV2[]> GetOrdersByStatus(OrderStatus status)
    {
        try
        {
            var orders = await _redisRepository.LoadAllCopyOrders();
            return [.. orders.Values.Where(o => o.OriginalOrder.Status == status)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders by status from Redis");
            return [];
        }
    }

    /// <summary>
    /// Получить копируемые ордера по OriginalOrderId (ID ордера трейдера)
    /// </summary>
    public Task<CopyOrderV2[]> GetOrdersByOriginalOrderId(long originalOrderId)
    {
        try
        {
            return _redisRepository.GetCopyOrdersByOriginalId(originalOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders by original order ID from Redis");
            return Task.FromResult(Array.Empty<CopyOrderV2>());
        }
    }

    /// <summary>
    /// Получить копируемые ордера по кошельку и символу
    /// </summary>
    public Task<CopyOrderV2[]> GetOrdersByWalletAndSymbol(Wallet wallet, string symbol)
    {
        try
        {
            return _redisRepository.GetCopyOrdersByWalletAndSymbol(wallet, symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders by wallet and symbol from Redis");
            return Task.FromResult(Array.Empty<CopyOrderV2>());
        }
    }

    /// <summary>
    /// Очистить все копируемые ордера
    /// </summary>
    public async Task ClearAllOrders()
    {
        try
        {
            var orders = await _redisRepository.LoadAllCopyOrders();
            var count = orders.Count;

            if (count > 0)
            {
                await _redisRepository.DeleteCopyOrders(orders.Keys);
            }

            _logger.LogInformation($"Все копируемые ордера очищены (было {count})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all copy orders from Redis");
        }
    }

    /// <summary>
    /// Получить статистику по копируемым ордерам
    /// </summary>
    public async Task<Dictionary<string, int>> GetStatistics()
    {
        try
        {
            var orders = await _redisRepository.LoadAllCopyOrders();
            var ordersList = orders.Values.ToList();

            return new Dictionary<string, int>
            {
                ["Total"] = ordersList.Count,
                ["Open"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.Open),
                ["Filled"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.Filled),
                ["Canceled"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.Canceled),
                ["Triggered"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.Triggered),
                ["Rejected"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.Rejected),
                ["MarginCanceled"] = ordersList.Count(o => o.OriginalOrder.Status == OrderStatus.MarginCanceled)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders statistics from Redis");
            return new Dictionary<string, int>
            {
                ["Total"] = 0,
                ["Open"] = 0,
                ["Filled"] = 0,
                ["Canceled"] = 0,
                ["Triggered"] = 0,
                ["Rejected"] = 0,
                ["MarginCanceled"] = 0
            };
        }
    }

    /// <summary>
    /// Проверяет необходимость очистки старых ордеров и выполняет её при необходимости
    /// Удаляет старые копируемые ордера, начиная с самых старых, пока количество не уменьшится до целевого
    /// </summary>
    private async Task CleanupOldOrdersIfNeeded()
    {
        try
        {
            var orders = await _redisRepository.LoadAllCopyOrders();

            // Проверяем, превышен ли порог
            if (orders.Count <= MaxOrdersThreshold)
            {
                return;
            }

            _logger.LogInformation($"CopyOrderStorageService: Начинаем очистку старых ордеров. Текущее количество: {orders.Count}");

            // Получаем все ордера, отсортированные по времени (от старых к новым)
            var ordersToRemove = orders.Values
                .OrderBy(order => order.OriginalOrder.Time)
                .Take(orders.Count - TargetOrdersCount)
                .ToList();

            _logger.LogInformation($"CopyOrderStorageService: Будет удалено {ordersToRemove.Count} старых ордеров");

            if (ordersToRemove.Count > 0)
            {
                // Удаляем старые ордера batch-операцией
                var orderIdsToRemove = ordersToRemove.Select(o => o.OrderId);
                await _redisRepository.DeleteCopyOrders(orderIdsToRemove);

                foreach (var order in ordersToRemove)
                {
                    _logger.LogDebug($"CopyOrderStorageService: Удален ордер ID={order.OrderId}, OriginalOrderId={order.OriginalOrderId}, " +
                                   $"Symbol={order.OriginalOrder.Symbol}, Time={order.OriginalOrder.Time}, Status={order.OriginalOrder.Status}");
                }
            }

            var remainingCount = orders.Count - ordersToRemove.Count;
            _logger.LogInformation($"CopyOrderStorageService: Очистка завершена. Удалено: {ordersToRemove.Count} ордеров. Осталось: {remainingCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old copy orders from Redis");
        }
    }
}
