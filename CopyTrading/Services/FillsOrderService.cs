using CopyTrading.DataEvents;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;
using System.Threading.Tasks;

namespace CopyTrading.Services;

public class FillsOrderService : IFillsOrderService
{
    private readonly ILogger<FillsOrderService> _logger;
    private readonly IRedisRepository _redisRepository;

    private readonly OrderStatus[] _orderFinalStatuses = [OrderStatus.Canceled, OrderStatus.Filled, OrderStatus.Rejected];

    private const decimal ExpectedCanceledQuantity = 0;  // Ожидаемое количество для отмененного ордера

    public FillsOrderService(
        ILogger<FillsOrderService> logger,
        IRedisRepository redisRepository)
    {
        _logger = logger;
        _redisRepository = redisRepository;

        _logger.LogInformation("FillsOrderService initialized - all data stored in Redis");
    }

    public async Task<OrderFills[]> GetAllOrderFills()
    {
        var allOrders = await _redisRepository.LoadAllOrders();
        return [.. allOrders.Values];
    }

    /// <summary>
    /// Получает OrderFills для указанного orderId
    /// </summary>
    /// <param name="orderId">ID ордера</param>
    /// <returns>OrderFills или null если ордер не найден</returns>
    public async Task<OrderFills?> GetOrderFillsByOrderId(long orderId)
    {
        return await _redisRepository.GetOrder(orderId);
    }

    /// <summary>
    /// Получает все открытые ордера для указанного кошелька
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <returns>Массив открытых OrderFills для указанного кошелька</returns>
    public async Task<OrderFills[]> GetOpenOrdersByWallet(Models.Values.Wallet wallet)
    {
        var allOrders = await _redisRepository.LoadAllOrders();
        return allOrders.Values
            .Where(orderFills => IsOpenOrderForWallet(orderFills, wallet))
            .ToArray();
    }

    /// <summary>
    /// Получает все pending (незавершенные) ордера для указанного кошелька и символа
    /// </summary>
    /// <param name="wallet">Кошелек для фильтрации</param>
    /// <param name="symbol">Символ для фильтрации</param>
    /// <returns>Массив pending OrderFills для указанного кошелька и символа</returns>
    public async Task<OrderFills[]> GetPendingOrdersByWalletAndSymbol(Models.Values.Wallet wallet, string symbol)
    {
        return await _redisRepository.GetPendingOrdersByWalletAndSymbol(wallet.Value, symbol);
    }

    /// <summary>
    /// Обновляет OrderSubType для указанного ордера
    /// </summary>
    /// <param name="orderId">ID ордера</param>
    /// <param name="newSubType">Новый SubType</param>
    /// <returns>True если обновление успешно, false если ордер не найден</returns>
    public async Task<bool> UpdateOrderSubType(long orderId, OrderSubType newSubType)
    {
        var result = await _redisRepository.UpdateOrderSubType(orderId, newSubType);

        if (result)
        {
            var orderFills = await _redisRepository.GetOrder(orderId);
            if (orderFills != null)
            {
                _logger.LogInformation(
                    $"FillsOrderService.UpdateOrderSubType: OrderId={orderId} -> {newSubType}, " +
                    $"Wallet={orderFills.OriginalOrder.Wallet}, Symbol={orderFills.OriginalOrder.Symbol}");
            }
        }
        else
        {
            _logger.LogWarning($"FillsOrderService.UpdateOrderSubType: OrderId={orderId} Ордер не найден");
        }

        return result;
    }

    /// <summary>
    /// Добавляет исторические (открытые) ордера в систему.
    /// Используется при первичной инициализации для загрузки ордеров, которые уже были открыты до подписки.
    /// Игнорирует ордера, которые уже есть в системе (по OrderId).
    /// НЕ вызывает события, НЕ сохраняет в БД, НЕ отправляет в UI.
    /// </summary>
    /// <param name="orders">Массив исторических ордеров</param>
    /// <returns>Количество добавленных ордеров</returns>
    public async Task<int> AddHistoricalOrders(OriginalOrder[] orders)
    {
        int addedCount = 0;
        int skippedCount = 0;

        foreach (var order in orders)
        {
            // Проверяем, существует ли ордер в Redis
            bool exists = await _redisRepository.OrderExists(order.OrderId);
            if (exists)
            {
                skippedCount++;
                _logger.LogDebug($"FillsOrderService.AddHistoricalOrders: OrderId={order.OrderId} Уже существует, пропускаем");
                continue;
            }

            // Создаем новый OrderFills для исторического ордера
            var orderFills = new OrderFills(order);

            // Сохраняем в Redis
            await _redisRepository.SaveOrder(orderFills);
            addedCount++;
        }

        _logger.LogInformation(
            $"FillsOrderService.AddHistoricalOrders: Добавлено {addedCount} исторических ордеров, пропущено {skippedCount} дубликатов");

        return addedCount;
    }

    public async Task OnNewOrders(OriginalOrder[] orders)
    {
        foreach (var newOrder in orders)
        {
            var orderFills = await _redisRepository.GetOrder(newOrder.OrderId);

            if (orderFills != null)
            {
                await ProcessExistingOrder(newOrder, orderFills);
            }
            else
            {
                await CreateNewOrderFills(newOrder);
            }

            await OnOrderFinished(newOrder);
        }
    }

    public async void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) trades)
    {
        if (trades.IsSnapshot)
        {
            LogTradeSnapshot();
            return;
        }

        foreach (var trade in trades.Trades)
        {
            LogNewTrade(trade);
            await ProcessTrade(trade);
        }
    }

    private async Task OnOrderFinished(OriginalOrder order)
    {
        _logger.LogInformation($"FillsOrderService.OnOrderFinished: OrderId={order.OrderId} Status={order.Status}");

        if (!IsFinalStatus(order.Status)) return;

        var orderFills = await _redisRepository.GetOrder(order.OrderId);
        if (orderFills == null)
        {
            _logger.LogWarning($"FillsOrderService.OnOrderFinished: OrderId={order.OrderId} OrderFills не найден");
            return;
        }

        switch (order.Status)
        {
            case OrderStatus.Filled:
                await HandleFilledOrder(order, orderFills);
                break;

            case OrderStatus.Canceled:
                await HandleCanceledOrder(order, orderFills);
                break;

            default:
                await HandleUnknownFinalStatus(order);
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

    private async Task AddTradesFromPending(OrderFills newOrderFills)
    {
        var allPendingTrades = await _redisRepository.LoadAllPendingTrades();
        if (allPendingTrades == null || allPendingTrades.Count == 0)
        {
            return;
        }

        var pendingTradesForOrder = allPendingTrades.Values
            .Where(t => t.OrderId == newOrderFills.OriginalOrder.OrderId)
            .ToArray();

        foreach (var pendingTrade in pendingTradesForOrder)
        {
            newOrderFills.Trades.Add(pendingTrade);
            await _redisRepository.DeletePendingTrade(pendingTrade.TradeId);
        }

        if (pendingTradesForOrder.Length > 0)
        {
            LogPendingTradesAdded(newOrderFills.OriginalOrder.OrderId, pendingTradesForOrder.Length);
        }
    }

    /// <summary>
    /// Обрабатывает трейд (новый или из pending)
    /// </summary>
    private async Task ProcessTrade(OriginalTrade trade)
    {
        var orderFills = await _redisRepository.GetOrder(trade.OrderId);

        if (orderFills != null)
        {
            await ProcessTradeForExistingOrder(trade, orderFills);
        }
        else
        {
            await AddTradeToPending(trade);
        }
    }

    /// <summary>
    /// Обрабатывает существующий ордер при получении обновления
    /// </summary>
    private async Task ProcessExistingOrder(OriginalOrder newOrder, OrderFills orderFills)
    {
        var orderChanges = GetChangesInOrder(newOrder, orderFills.OriginalOrder);
        orderFills.UpdateOrder(newOrder);

        // Сохранить в Redis
        await _redisRepository.SaveOrder(orderFills);
    }

    /// <summary>
    /// Создает новый OrderFills и добавляет pending трейды
    /// </summary>
    private async Task CreateNewOrderFills(OriginalOrder newOrder)
    {
        var newOrderFills = new OrderFills(newOrder);

        await AddTradesFromPending(newOrderFills);

        // Сохранить в Redis
        await _redisRepository.SaveOrder(newOrderFills);
    }

    /// <summary>
    /// Обрабатывает трейд для существующего ордера
    /// </summary>
    private async Task ProcessTradeForExistingOrder(OriginalTrade trade, OrderFills orderFills)
    {
        if (IsDuplicateTrade(orderFills, trade))
        {
            LogDuplicateTrade(trade);
            return;
        }

        orderFills.Trades.Add(trade);
        await _redisRepository.SaveOrder(orderFills);
        LogTradeAdded(trade, orderFills);
        await OnOrderFinished(orderFills.OriginalOrder);
    }

    /// <summary>
    /// Добавляет трейд в pending если ордер еще не получен
    /// </summary>
    private async Task AddTradeToPending(OriginalTrade trade)
    {
        bool exists = await _redisRepository.PendingTradeExists(trade.TradeId);
        if (!exists)
        {
            await _redisRepository.SavePendingTrade(trade);
        }
        LogOrderNotFoundForTrade(trade);
    }

    /// <summary>
    /// Удаляет ордер из списка ошибок
    /// </summary>
    private async Task RemoveOrderError(long orderId)
    {
        bool hasError = await _redisRepository.OrderHasError(orderId);
        if (hasError)
        {
            await _redisRepository.DeleteOrderError(orderId);
        }
    }

    /// <summary>
    /// Добавляет ордер в список ошибок
    /// </summary>
    private async Task AddOrderError(long orderId, string errorMessage)
    {
        bool hasError = await _redisRepository.OrderHasError(orderId);
        if (!hasError)
        {
            await _redisRepository.SaveOrderError(orderId, errorMessage);
        }
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


    #region Order Status Handlers

    /// <summary>
    /// Обрабатывает ордер со статусом Filled
    /// </summary>
    private async Task HandleFilledOrder(OriginalOrder order, OrderFills orderFills)
    {
        if (IsQuantityMatched(orderFills, order.Quantity))
        {
            // Успех: весь Order заполнен/выполнен, соответствует статусу
            PublishOrderFinished(orderFills);
            await RemoveOrderError(order.OrderId);
            _logger.LogInformation($"FillsOrderService.HandleFilledOrder: OrderId={order.OrderId} Success");
        }
        else
        {
            // Ошибка: сумма всех trades не соответствует order.Quantity
            string error = $"FillsOrderService.HandleFilledOrder: OrderId={order.OrderId} Не сходится сумма всех trades и order.Quantity";
            await AddOrderError(order.OrderId, error);
        }

        // Сохранить в Redis
        await _redisRepository.SaveOrder(orderFills);
    }

    /// <summary>
    /// Обрабатывает ордер со статусом Canceled
    /// </summary>
    private async Task HandleCanceledOrder(OriginalOrder order, OrderFills orderFills)
    {
        PublishOrderFinished(orderFills);
        await RemoveOrderError(order.OrderId);

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

        // Сохранить в Redis
        await _redisRepository.SaveOrder(orderFills);
    }

    /// <summary>
    /// Обрабатывает ордер с неизвестным финальным статусом (Rejected и др.)
    /// </summary>
    private async Task HandleUnknownFinalStatus(OriginalOrder order)
    {
        string error = $"Неизвестная ошибка";
        await AddOrderError(order.OrderId, error);
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
