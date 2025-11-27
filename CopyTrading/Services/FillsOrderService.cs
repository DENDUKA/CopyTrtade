using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

public class FillsOrderService(ILogger<FillsOrderService> _logger) : IFillsOrderService
{
    private readonly ConcurrentDictionary<long, OrderFills> _orders = [];
    private readonly ConcurrentDictionary<long, OriginalTrade> _pendingTrades = [];
    internal readonly ConcurrentDictionary<long, string> _ordersWithError = [];

    private readonly OrderStatus[] _orderFinalStatuses = [OrderStatus.Canceled, OrderStatus.Filled, OrderStatus.Rejected];

    // Константы для управления размером кэша
    private const int MaxOrdersThreshold = 2000;  // Порог для начала очистки
    private const int TargetOrdersCount = 500;    // Целевое количество после очистки
    private const decimal ExpectedCanceledQuantity = 0;  // Ожидаемое количество для отмененного ордера

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
            .Where(orderFills => IsOpenOrderForWallet(orderFills, wallet))
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
            .Where(orderFills => IsPendingOrderForWalletAndSymbol(orderFills, wallet, symbol))
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
                $"FillsOrderService.UpdateOrderSubType: OrderId={orderId} {oldSubType} -> {newSubType}, " +
                $"Wallet={orderFills.OriginalOrder.Wallet}, Symbol={orderFills.OriginalOrder.Symbol}");

            return true;
        }

        _logger.LogWarning($"FillsOrderService.UpdateOrderSubType: OrderId={orderId} Ордер не найден");
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
                _logger.LogDebug($"FillsOrderService.AddHistoricalOrders: OrderId={order.OrderId} Уже существует, пропускаем");
                continue;
            }

            // Создаем новый OrderFills для исторического ордера
            var orderFills = new OrderFills(order);

            _orders.TryAdd(order.OrderId, orderFills);
            addedCount++;
        }

        _logger.LogInformation(
            $"FillsOrderService.AddHistoricalOrders: Добавлено {addedCount} исторических ордеров, пропущено {skippedCount} дубликатов");

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
        if (trades.IsSnapshot)
        {
            LogTradeSnapshot();
            return;
        }

        foreach (var trade in trades.Trades)
        {
            LogNewTrade(trade);
            ProcessTrade(trade);
        }
    }

    private void OnOrderFinished(OriginalOrder order)
    {
        _logger.LogInformation($"FillsOrderService.OnOrderFinished: OrderId={order.OrderId} Status={order.Status}");

        if (!IsFinalStatus(order.Status)) return;

        if (!_orders.TryGetValue(order.OrderId, out var orderFills))
        {
            _logger.LogWarning($"FillsOrderService.OnOrderFinished: OrderId={order.OrderId} OrderFills не найден");
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
            _logger.LogInformation($"FillsOrderService.LogOrderChanges: OrderId={orderId} Нет изменений");
        }
        else
        {
            _logger.LogInformation($"FillsOrderService.LogOrderChanges: OrderId={orderId} Изменения:\n{changesString}");
        }
    }

    private void AddTradesFromPending(OrderFills newOrderFills)
    {
        var pendingTradesForOrder = _pendingTrades.Values
            .Where(t => t.OrderId == newOrderFills.OriginalOrder.OrderId)
            .ToArray();

        foreach (var pendingTrade in pendingTradesForOrder)
        {
            newOrderFills.Trades.Add(pendingTrade);
            _pendingTrades.TryRemove(pendingTrade.TradeId, out _);
        }

        if (pendingTradesForOrder.Length > 0)
        {
            LogPendingTradesAdded(newOrderFills.OriginalOrder.OrderId, pendingTradesForOrder.Length);
        }
    }

    /// <summary>
    /// Обрабатывает трейд (новый или из pending)
    /// </summary>
    private void ProcessTrade(OriginalTrade trade)
    {
        if (_orders.TryGetValue(trade.OrderId, out var orderFills))
        {
            ProcessTradeForExistingOrder(trade, orderFills);
        }
        else
        {
            AddTradeToPending(trade);
        }
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
            _logger.LogError($"FillsOrderService.CreateNewOrderFills: OrderId={newOrder.OrderId} Не удалось добавить ордер");
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
            LogDuplicateTrade(trade);
            return;
        }

        orderFills.Trades.Add(trade);
        LogTradeAdded(trade, orderFills);
        OnOrderFinished(orderFills.OriginalOrder);
    }

    /// <summary>
    /// Добавляет трейд в pending если ордер еще не получен
    /// </summary>
    private void AddTradeToPending(OriginalTrade trade)
    {
        _pendingTrades.TryAdd(trade.TradeId, trade);
        LogOrderNotFoundForTrade(trade);
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
        _logger.LogError($"FillsOrderService.AddOrderError: OrderId={orderId} {errorMessage}");
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

        _logger.LogInformation($"FillsOrderService.CleanupOldCompletedOrdersIfNeeded: Начинаем очистку, текущее количество {_orders.Count}");

        // Получаем все завершенные ордера, отсортированные по времени (от старых к новым)
        var completedOrders = _orders.Values
            .Where(orderFills => IsFinalStatus(orderFills.OriginalOrder.Status))
            .OrderBy(orderFills => orderFills.OriginalOrder.Time)
            .ToList();

        _logger.LogInformation($"FillsOrderService.CleanupOldCompletedOrdersIfNeeded: Найдено {completedOrders.Count} завершенных ордеров");

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
                _logger.LogDebug($"FillsOrderService.CleanupOldCompletedOrdersIfNeeded: OrderId={orderFills.OriginalOrder.OrderId} Удален завершенный ордер");
            }
        }

        _logger.LogInformation($"FillsOrderService.CleanupOldCompletedOrdersIfNeeded: Очистка завершена, удалено {removedCount}, осталось {_orders.Count}");
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
            _logger.LogInformation($"FillsOrderService.HandleFilledOrder: OrderId={order.OrderId} Success");
        }
        else
        {
            // Ошибка: сумма всех trades не соответствует order.Quantity
            string error = $"FillsOrderService.HandleFilledOrder: OrderId={order.OrderId} Не сходится сумма всех trades и order.Quantity";
            AddOrderError(order.OrderId, error);
        }
    }

    /// <summary>
    /// Обрабатывает ордер со статусом Canceled
    /// </summary>
    private void HandleCanceledOrder(OriginalOrder order, OrderFills orderFills)
    {
        PublishOrderFinished(orderFills);
        RemoveOrderError(order.OrderId);

        if (IsQuantityMatched(orderFills, ExpectedCanceledQuantity))
        {
            // Закрытие Order: ордер не был выполнен
            _logger.LogInformation($"FillsOrderService.HandleCanceledOrder: OrderId={order.OrderId} Success");
        }
        else
        {
            // Закрытие Order: ордер был выполнен частично
            _logger.LogWarning($"FillsOrderService.HandleCanceledOrder: OrderId={order.OrderId} Ордер был выполнен частично");
        }
    }

    /// <summary>
    /// Обрабатывает ордер с неизвестным финальным статусом (Rejected и др.)
    /// </summary>
    private void HandleUnknownFinalStatus(OriginalOrder order)
    {
        string error = $"Неизвестная ошибка";
        AddOrderError(order.OrderId, error);
        _logger.LogWarning($"FillsOrderService.HandleUnknownFinalStatus: OrderId={order.OrderId} {error}");
    }

    #endregion

    #region Validation and Predicates

    /// <summary>
    /// Проверяет является ли статус финальным
    /// </summary>
    private bool IsFinalStatus(OrderStatus status)
    {
        return _orderFinalStatuses.Contains(status);
    }

    /// <summary>
    /// Проверяет является ли ордер открытым для указанного кошелька
    /// </summary>
    private bool IsOpenOrderForWallet(OrderFills orderFills, Models.Values.Wallet wallet)
    {
        return orderFills.OriginalOrder.Wallet.Value == wallet.Value &&
               !IsFinalStatus(orderFills.OriginalOrder.Status);
    }

    /// <summary>
    /// Проверяет является ли ордер pending для указанного кошелька и символа
    /// </summary>
    private bool IsPendingOrderForWalletAndSymbol(OrderFills orderFills, Models.Values.Wallet wallet, string symbol)
    {
        return orderFills.OriginalOrder.Wallet.Value == wallet.Value &&
               orderFills.OriginalOrder.Symbol == symbol &&
               !IsFinalStatus(orderFills.OriginalOrder.Status);
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

    #endregion

    #region Logging Helpers

    private void LogNewTrade(OriginalTrade trade)
    {
        _logger.LogInformation($"FillsOrderService.LogNewTrade: OrderId={trade.OrderId} TradeId={trade.TradeId} Новый trade");
    }

    private void LogTradeSnapshot()
    {
        _logger.LogDebug("FillsOrderService.LogTradeSnapshot: Пропускаем snapshot");
    }

    private void LogDuplicateTrade(OriginalTrade trade)
    {
        _logger.LogInformation($"FillsOrderService.LogDuplicateTrade: OrderId={trade.OrderId} TradeId={trade.TradeId} Дубликат trade");
    }

    private void LogTradeAdded(OriginalTrade trade, OrderFills orderFills)
    {
        _logger.LogInformation($"FillsOrderService.LogTradeAdded: OrderId={trade.OrderId} FillStatus={orderFills.FillStatus}");
    }

    private void LogOrderNotFoundForTrade(OriginalTrade trade)
    {
        _logger.LogWarning($"FillsOrderService.LogOrderNotFoundForTrade: OrderId={trade.OrderId} Ордер не найден");
    }

    private void LogPendingTradesAdded(long orderId, int count)
    {
        _logger.LogInformation($"FillsOrderService.LogPendingTradesAdded: OrderId={orderId} Добавлено {count} pending трейдов");
    }

    #endregion
}
