using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

public class FillsOrderService(ILogger<FillsOrderService> _logger)
{
    private readonly ConcurrentDictionary<long, OrderFills> _orders = [];
    private readonly ConcurrentDictionary<long, OriginalTrade> _pendingTrades = [];

    internal readonly ConcurrentDictionary<long, string> _ordersWithError = [];

    private readonly OrderStatus[] _orderFinalStatuses = [OrderStatus.Canceled, OrderStatus.Filled, OrderStatus.Rejected];

    // Константы для управления размером кэша
    private const int MaxOrdersThreshold = 2000;  // Порог для начала очистки
    private const int TargetOrdersCount = 500;    // Целевое количество после очистки

    public OrderFills[] GetAllOrderFills()
    {
        return [.. _orders.Values];
    }

    /// <summary>
    /// Получает OrderFills для указанного orderId
    /// </summary>
    /// <param name="orderId">ID ордера</param>
    /// <returns>OrderFills или null если ордер не найден</returns>
    public OrderFills? GetOrderFillsByOrderId(long orderId)
    {
        return _orders.TryGetValue(orderId, out var orderFills) ? orderFills : null;
    }

    /// <summary>
    /// Получает все открытые ордера для указанного кошелька
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <returns>Массив открытых OrderFills для указанного кошелька</returns>
    public OrderFills[] GetOpenOrdersByWallet(Models.Values.Wallet wallet)
    {
        return _orders.Values
            .Where(orderFills =>
                orderFills.OriginalOrder.Wallet.Value == wallet.Value &&
                !IsFinalStatus(orderFills.OriginalOrder.Status))
            .ToArray();
    }

    /// <summary>
    /// Получает все pending (незавершенные) ордера для указанного кошелька и символа
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <returns>Массив pending OrderFills для указанного кошелька и символа</returns>
    public OrderFills[] GetPendingOrdersByWalletAndSymbol(Models.Values.Wallet wallet, string symbol)
    {
        return _orders.Values
            .Where(orderFills =>
                orderFills.OriginalOrder.Wallet.Value == wallet.Value &&
                orderFills.OriginalOrder.Symbol == symbol &&
                !IsFinalStatus(orderFills.OriginalOrder.Status))
            .ToArray();
    }

    /// <summary>
    /// Обновляет OrderSubType для указанного ордера
    /// </summary>
    /// <param name="orderId">ID ордера</param>
    /// <param name="newSubType">Новый SubType</param>
    /// <returns>True если обновление успешно, false если ордер не найден</returns>
    public bool UpdateOrderSubType(long orderId, OrderSubType newSubType)
    {
        if (_orders.TryGetValue(orderId, out var orderFills))
        {
            var oldSubType = orderFills.OriginalOrder.SubType;
            orderFills.OriginalOrder.SubType = newSubType;

            _logger.LogInformation(
                $"OrderSubType обновлен для OriginalOrder ID={orderId}: {oldSubType} -> {newSubType}, " +
                $"Wallet={orderFills.OriginalOrder.Wallet}, Symbol={orderFills.OriginalOrder.Symbol}");

            return true;
        }

        _logger.LogWarning($"Ордер ID={orderId} не найден для обновления SubType");
        return false;
    }

    /// <summary>
    /// Добавляет исторические (открытые) ордера в систему.
    /// Используется при первичной инициализации для загрузки ордеров, которые уже были открыты до подписки.
    /// Игнорирует ордера, которые уже есть в системе (по OrderId).
    /// НЕ вызывает события, НЕ сохраняет в БД, НЕ отправляет в UI.
    /// </summary>
    /// <param name="orders">Массив исторических ордеров</param>
    /// <returns>Количество добавленных ордеров</returns>
    public int AddHistoricalOrders(OriginalOrder[] orders)
    {
        int addedCount = 0;
        int skippedCount = 0;

        foreach (var order in orders)
        {
            // Игнорируем ордера, которые уже есть в системе
            if (_orders.ContainsKey(order.OrderId))
            {
                skippedCount++;
                _logger.LogDebug($"AddHistoricalOrders: Ордер ID={order.OrderId} уже существует, пропускаем");
                continue;
            }

            // Создаем новый OrderFills для исторического ордера
            var orderFills = new OrderFills(order);

            _orders.TryAdd(order.OrderId, orderFills);
            addedCount++;
        }

        _logger.LogInformation(
            $"AddHistoricalOrders: Добавлено {addedCount} исторических ордеров, пропущено {skippedCount} дубликатов");

        return addedCount;
    }

    public void OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var newOrder in orders)
        {
            if (_orders.TryGetValue(newOrder.OrderId, out var orderFills))
            {
                ProcessExistingOrder(newOrder, orderFills);
            }
            else
            {
                CreateNewOrderFills(newOrder);
            }

            OnOrderFinished(newOrder);
        }

        // Проверяем необходимость очистки после обработки ордеров
        CleanupOldCompletedOrdersIfNeeded();
    }

    public void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades)
    {
        if (trades.IsSnapshot) return;

        foreach (var trade in trades.Trades)
        {
            _logger.LogInformation($"FillsOrderService OnNewTrades: Новый trade {trade}");

            if (_orders.TryGetValue(trade.OrderId, out var orderFills))
            {
                ProcessTradeForExistingOrder(trade, orderFills);
            }
            else
            {
                AddTradeToPending(trade);
            }
        }
    }

    private void OnOrderFinished(OriginalOrder order)
    {
        _logger.LogInformation($"OnOrderFinished {order.OrderId} status: {order.Status}");

        if (!IsFinalStatus(order.Status)) return;

        if (!_orders.TryGetValue(order.OrderId, out var orderFills))
        {
            _logger.LogWarning($"OnOrderFinished: OrderFills не найден для ордера {order.OrderId}");
            return;
        }

        switch (order.Status)
        {
            case OrderStatus.Filled:
                HandleFilledOrder(order, orderFills);
                break;

            case OrderStatus.Canceled:
                HandleCanceledOrder(order, orderFills);
                break;

            default:
                HandleUnknownFinalStatus(order);
                break;
        }
    }

    private OrderChanges GetChangesInOrder(OriginalOrder newOrder, OriginalOrder oldOrder)
    {
        var changes = OrderChanges.None;
        string changesString = string.Empty;

        // Сравнение всех свойств Order
        AppendChangeIfDifferent(ref changesString, "OrderId", oldOrder.OrderId, newOrder.OrderId);
        AppendChangeIfDifferent(ref changesString, "Wallet", oldOrder.Wallet?.Value, newOrder.Wallet?.Value);
        AppendChangeIfDifferent(ref changesString, "Symbol", oldOrder.Symbol, newOrder.Symbol);
        AppendChangeIfDifferent(ref changesString, "Price", oldOrder.Price, newOrder.Price);
        AppendChangeIfDifferent(ref changesString, "Size", oldOrder.Quantity, newOrder.Quantity);
        AppendChangeIfDifferent(ref changesString, "Leverage", oldOrder.Leverage, newOrder.Leverage);
        AppendChangeIfDifferent(ref changesString, "SubType", oldOrder.SubType, newOrder.SubType);
        AppendChangeIfDifferent(ref changesString, "Value", oldOrder.VolumeUsd, newOrder.VolumeUsd);

        // Direction - требует установки флага
        if (newOrder.Direction != oldOrder.Direction)
        {
            AppendChangeIfDifferent(ref changesString, "Direction", oldOrder.Direction, newOrder.Direction);
            changes |= OrderChanges.Direction;
        }

        // Status - требует установки флага
        if (newOrder.Status != oldOrder.Status)
        {
            AppendChangeIfDifferent(ref changesString, "Status", oldOrder.Status, newOrder.Status);
            changes |= OrderChanges.Status;
        }

        LogOrderChanges(newOrder.OrderId, changesString);

        return changes;
    }

    /// <summary>
    /// Логирует изменения в ордере
    /// </summary>
    private void LogOrderChanges(long orderId, string changesString)
    {
        if (string.IsNullOrEmpty(changesString))
        {
            _logger.LogInformation($"Изменения в ордере {orderId}: нет");
        }
        else
        {
            _logger.LogInformation($"Изменения в ордере {orderId}:\n{changesString}");
        }
    }

    private void AddTradesFromPending(OrderFills newOrderFills)
    {
        foreach (var pendingTrade in _pendingTrades.Values.Where(t => t.OrderId == newOrderFills.OriginalOrder.OrderId))
        {
            newOrderFills.Trades.Add(pendingTrade);
            _pendingTrades.TryRemove(pendingTrade.TradeId, out _);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Проверяет является ли статус финальным
    /// </summary>
    private bool IsFinalStatus(OrderStatus status)
    {
        return _orderFinalStatuses.Contains(status);
    }

    /// <summary>
    /// Обрабатывает существующий ордер при получении обновления
    /// </summary>
    private void ProcessExistingOrder(OriginalOrder newOrder, OrderFills orderFills)
    {
        var orderChanges = GetChangesInOrder(newOrder, orderFills.OriginalOrder);
        orderFills.UpdateOrder(newOrder);
    }

    /// <summary>
    /// Создает новый OrderFills и добавляет pending трейды
    /// </summary>
    private void CreateNewOrderFills(OriginalOrder newOrder)
    {
        var newOrderFills = new OrderFills(newOrder);

        if (!_orders.TryAdd(newOrder.OrderId, newOrderFills))
        {
            _logger.LogError($"FillsOrderService OnNewOrders: не удалось добавить ордер {newOrder.OrderId}");
            return;
        }

        AddTradesFromPending(newOrderFills);
    }

    /// <summary>
    /// Обрабатывает трейд для существующего ордера
    /// </summary>
    private void ProcessTradeForExistingOrder(OriginalTrade trade, OrderFills orderFills)
    {
        if (IsDuplicateTrade(orderFills, trade))
        {
            _logger.LogInformation($"FillsOrderService OnNewTrades: Дубликат trade {trade.TradeId} для ордера {trade.OrderId}");
            return;
        }

        orderFills.Trades.Add(trade);
        _logger.LogInformation($"FillsOrderService OnNewTrades: ордер {trade.OrderId} fillStatus : {orderFills.FillStatus}");
        OnOrderFinished(orderFills.OriginalOrder);
    }

    /// <summary>
    /// Добавляет трейд в pending если ордер еще не получен
    /// </summary>
    private void AddTradeToPending(OriginalTrade trade)
    {
        _pendingTrades.TryAdd(trade.TradeId, trade);
        _logger.LogWarning($"FillsOrderService OnNewTrades: не удалось найти ордер {trade.OrderId}");
    }

    /// <summary>
    /// Проверяет является ли трейд дубликатом
    /// </summary>
    private static bool IsDuplicateTrade(OrderFills orderFills, OriginalTrade trade)
    {
        return orderFills.Trades.Any(x => x.TradeId == trade.TradeId);
    }

    /// <summary>
    /// Проверяет соответствует ли заполненное количество ожидаемому
    /// </summary>
    private static bool IsQuantityMatched(OrderFills orderFills, decimal expectedQuantity)
    {
        return orderFills.FilledQuantity == expectedQuantity;
    }

    /// <summary>
    /// Удаляет ордер из списка ошибок
    /// </summary>
    private void RemoveOrderError(long orderId)
    {
        _ordersWithError.Remove(orderId, out var _);
    }

    /// <summary>
    /// Добавляет ордер в список ошибок
    /// </summary>
    private void AddOrderError(long orderId, string errorMessage)
    {
        _ordersWithError.TryAdd(orderId, errorMessage);
        _logger.LogError(errorMessage);
    }

    /// <summary>
    /// Публикует событие завершения ордера
    /// </summary>
    private static void PublishOrderFinished(OrderFills orderFills)
    {
        DataBusEvents.OrderFinished?.Invoke(orderFills);
    }

    /// <summary>
    /// Добавляет строку изменения если значения различаются
    /// </summary>
    private static void AppendChangeIfDifferent<T>(ref string changes, string propertyName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changes += $"{propertyName}: {oldValue} -> {newValue};\n";
        }
    }

    #endregion

    #region Memory Management

    /// <summary>
    /// Проверяет необходимость очистки старых завершенных ордеров и выполняет её при необходимости
    /// </summary>
    private void CleanupOldCompletedOrdersIfNeeded()
    {
        // Проверяем, превышен ли порог
        if (_orders.Count <= MaxOrdersThreshold)
        {
            return;
        }

        _logger.LogInformation($"Начинаем очистку старых ордеров. Текущее количество: {_orders.Count}");

        // Получаем все завершенные ордера, отсортированные по времени (от старых к новым)
        var completedOrders = _orders.Values
            .Where(orderFills => IsFinalStatus(orderFills.OriginalOrder.Status))
            .OrderBy(orderFills => orderFills.OriginalOrder.Time)
            .ToList();

        _logger.LogInformation($"Найдено {completedOrders.Count} завершенных ордеров");

        // Вычисляем сколько нужно удалить
        int ordersToRemove = _orders.Count - TargetOrdersCount;
        int removedCount = 0;

        // Удаляем старые завершенные ордера
        foreach (var orderFills in completedOrders)
        {
            if (removedCount >= ordersToRemove)
            {
                break;
            }

            if (_orders.TryRemove(orderFills.OriginalOrder.OrderId, out _))
            {
                removedCount++;
                _ordersWithError.TryRemove(orderFills.OriginalOrder.OrderId, out _);
                _logger.LogDebug($"Удален завершенный ордер {orderFills.OriginalOrder.OrderId} (время: {orderFills.OriginalOrder.Time})");
            }
        }

        _logger.LogInformation($"Очистка завершена. Удалено: {removedCount} ордеров. Осталось: {_orders.Count}");
    }

    #endregion

    #region Order Status Handlers

    /// <summary>
    /// Обрабатывает ордер со статусом Filled
    /// </summary>
    private void HandleFilledOrder(OriginalOrder order, OrderFills orderFills)
    {
        if (IsQuantityMatched(orderFills, order.Quantity))
        {
            // Успех: весь Order заполнен/выполнен, соответствует статусу
            PublishOrderFinished(orderFills);
            RemoveOrderError(order.OrderId);
            _logger.LogInformation($"OnOrderFinished Filled {order.OrderId} Success");
        }
        else
        {
            // Ошибка: сумма всех trades не соответствует order.Quantity
            string error = $"OnOrderFinished Filled {order.OrderId} не сходится сумма всех trades и order.Quantity";
            AddOrderError(order.OrderId, error);
        }
    }

    /// <summary>
    /// Обрабатывает ордер со статусом Canceled
    /// </summary>
    private void HandleCanceledOrder(OriginalOrder order, OrderFills orderFills)
    {
        if (IsQuantityMatched(orderFills, 0))
        {
            // Закрытие Order: ордер не был выполнен
            PublishOrderFinished(orderFills);
            RemoveOrderError(order.OrderId);
            _logger.LogInformation($"OnOrderFinished Canceled {order.OrderId} Success");
        }
        else
        {
            // Закрытие Order: ордер был выполнен частично
            PublishOrderFinished(orderFills);
            RemoveOrderError(order.OrderId);
            _logger.LogWarning($"OnOrderFinished Canceled {order.OrderId} Order был выполнен частично");
        }
    }

    /// <summary>
    /// Обрабатывает ордер с неизвестным финальным статусом (Rejected и др.)
    /// </summary>
    private void HandleUnknownFinalStatus(OriginalOrder order)
    {
        string error = $"Неизвестная ошибка OrderId: {order.OrderId}";
        AddOrderError(order.OrderId, error);
        _logger.LogWarning(error);
    }

    #endregion
}
