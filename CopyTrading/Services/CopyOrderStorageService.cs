using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для хранения и управления копируемыми ордерами
/// Отслеживает все ордера, которые мы создаем на основе ордеров трейдеров
/// </summary>
public class CopyOrderStorageService : ICopyOrderStorageService
{
    private readonly ConcurrentDictionary<long, CopyOrderV2> _copyOrders = new();
    private readonly ILogger<CopyOrderStorageService> _logger;

    // Константы для управления размером кэша
    private const int MaxOrdersThreshold = 2000;  // Порог для начала очистки
    private const int TargetOrdersCount = 500;    // Целевое количество после очистки

    public CopyOrderStorageService(ILogger<CopyOrderStorageService> logger)
    {
        _logger = logger;

        // Подписываемся на события создания и закрытия копируемых ордеров
        DataBusEvents.CopyOrderCreated += OnCopyOrderCreated;
        DataBusEvents.CopyOrderClosed += OnCopyOrderClosed;
        DataBusEvents.CopyOrderFilled += OnCopyOrderFilled;
    }

    /// <summary>
    /// Обработчик события создания копируемого ордера
    /// </summary>
    private void OnCopyOrderCreated(CopyOrderV2 copyOrder)
    {
        _logger.LogInformation($"CopyOrderStorageService OnCopyOrderCreated {copyOrder.ToString()}");
        AddOrder(copyOrder);
    }

    private void OnCopyOrderFilled((OriginalOrder Order, OrderStatus Status) data)
    {
        _logger.LogInformation($"CopyOrderStorageService OnCopyOrderFilled {data.Order.ToString()}");
        CloseOrder(data.Order, data.Status);
    }

    /// <summary>
    /// Добавить копируемый ордер в хранилище
    /// </summary>
    public bool AddOrder(CopyOrderV2 order)
    {
        try
        {
            if (_copyOrders.TryAdd(order.OrderId, order))
            {
                _logger.LogInformation(
                    $"Копируемый ордер добавлен: ID={order.OrderId}, Symbol={order.OriginalOrder.Symbol}, " +
                    $"Direction={order.OriginalOrder.Direction}, Price={order.OriginalOrder.Price}, Quantity={order.Quantity}, " +
                    $"OriginalOrderId={order.OriginalOrderId}");

                // Проверяем необходимость очистки после добавления ордера
                CleanupOldOrdersIfNeeded();

                return true;
            }
            else
            {
                _logger.LogWarning($"Копируемый ордер с ID {order.OrderId} уже существует в хранилище");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при добавлении копируемого ордера ID={order.OrderId}");
            return false;
        }
    }

    /// <summary>
    /// Обработчик события закрытия копируемого ордера
    /// </summary>
    private void OnCopyOrderClosed((OriginalOrder Order, OrderStatus Status) data)
    {
        _logger.LogInformation($"CopyOrderStorageService OnCopyOrderClosed {data.Order.ToString()}");
        CloseOrder(data.Order, data.Status);
    }

    /// <summary>
    /// Получить все копируемые ордера
    /// </summary>
    public CopyOrderV2[] GetAllOrders()
    {
        return [.. _copyOrders.Values];
    }

    /// <summary>
    /// Получить копируемые ордера по статусу
    /// </summary>
    public CopyOrderV2[] GetOrdersByStatus(OrderStatus status)
    {
        return [.. _copyOrders.Values.Where(o => o.OriginalOrder.Status == status)];
    }

    /// <summary>
    /// Получить копируемые ордера по OriginalOrderId (ID ордера трейдера)
    /// </summary>
    public CopyOrderV2[] GetOrdersByOriginalOrderId(long originalOrderId)
    {
        return [.. _copyOrders.Values.Where(o => o.OriginalOrderId == originalOrderId)];
    }

    /// <summary>
    /// Закрыть все копируемые ордера по оригинальному ордеру (обновить статус на Canceled или Rejected)
    /// Используется для Canceled и Rejected ордеров
    /// </summary>
    public void CloseOrder(OriginalOrder originalOrder, OrderStatus closeStatus)
    {
        try
        {
            // Находим все копируемые ордера по OriginalOrderId
            var copyOrders = GetOrdersByOriginalOrderId(originalOrder.OrderId);

            if (copyOrders.Length == 0)
            {
                _logger.LogWarning(
                    $"Копируемые ордера не найдены для оригинального ордера ID={originalOrder.OrderId}, " +
                    $"Symbol={originalOrder.Symbol}, Direction={originalOrder.Direction}");

                return;
            }

            foreach (var copyOrder in copyOrders)
            {
                var oldStatus = copyOrder.OriginalOrder.Status;

                // Обновляем статус
                copyOrder.OriginalOrder.Status = closeStatus;

                _logger.LogInformation(
                    $"Копируемый ордер закрыт: ID={copyOrder.OrderId}, {oldStatus} -> {closeStatus}, " +
                    $"Symbol={copyOrder.OriginalOrder.Symbol}, Direction={copyOrder.OriginalOrder.Direction}, " +
                    $"OriginalOrderId={originalOrder.OrderId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при закрытии копируемых ордеров для OriginalOrderId={originalOrder.OrderId}");
        }
    }

    /// <summary>
    /// Очистить все копируемые ордера
    /// </summary>
    public void ClearAllOrders()
    {
        var count = _copyOrders.Count;
        _copyOrders.Clear();
        _logger.LogInformation($"Все копируемые ордера очищены (было {count})");
    }

    /// <summary>
    /// Получить статистику по копируемым ордерам
    /// </summary>
    public Dictionary<string, int> GetStatistics()
    {
        return new Dictionary<string, int>
        {
            ["Total"] = _copyOrders.Count,
            ["Open"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.Open),
            ["Filled"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.Filled),
            ["Canceled"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.Canceled),
            ["Triggered"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.Triggered),
            ["Rejected"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.Rejected),
            ["MarginCanceled"] = _copyOrders.Values.Count(o => o.OriginalOrder.Status == OrderStatus.MarginCanceled)
        };
    }

    /// <summary>
    /// Проверяет необходимость очистки старых ордеров и выполняет её при необходимости
    /// Удаляет старые копируемые ордера, начиная с самых старых, пока количество не уменьшится до целевого
    /// </summary>
    private void CleanupOldOrdersIfNeeded()
    {
        // Проверяем, превышен ли порог
        if (_copyOrders.Count <= MaxOrdersThreshold)
        {
            return;
        }

        _logger.LogInformation($"CopyOrderStorageService: Начинаем очистку старых ордеров. Текущее количество: {_copyOrders.Count}");

        // Получаем все ордера, отсортированные по времени (от старых к новым)
        var ordersToRemove = _copyOrders.Values
            .OrderBy(order => order.OriginalOrder.Time)
            .Take(_copyOrders.Count - TargetOrdersCount)
            .ToList();

        _logger.LogInformation($"CopyOrderStorageService: Будет удалено {ordersToRemove.Count} старых ордеров");

        int removedCount = 0;

        // Удаляем старые ордера
        foreach (var order in ordersToRemove)
        {
            if (_copyOrders.TryRemove(order.OrderId, out _))
            {
                removedCount++;
                _logger.LogDebug($"CopyOrderStorageService: Удален ордер ID={order.OrderId}, OriginalOrderId={order.OriginalOrderId}, " +
                               $"Symbol={order.OriginalOrder.Symbol}, Time={order.OriginalOrder.Time}, Status={order.OriginalOrder.Status}");
            }
        }

        _logger.LogInformation($"CopyOrderStorageService: Очистка завершена. Удалено: {removedCount} ордеров. Осталось: {_copyOrders.Count}");
    }
}
