using System.Collections.Concurrent;
using CopyTrading.Mappers;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

public class CurrentWalletPositionService(
    IWalletInfoProvider _walletInfoProvider,
    IFillsOrderService _fillsOrderService,
    ILogger<CurrentWalletPositionService> _logger) : ICurrentWalletPositionService
{
    private readonly ConcurrentDictionary<Wallet, WalletPositionsSnapshot> _walletPositionSnapshot = [];
    private readonly ConcurrentDictionary<Wallet, SemaphoreSlim> _walletSemaphores = [];

    public async Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        ArgumentNullException.ThrowIfNull(newTrades.Trades, nameof(newTrades.Trades));

        // Если это snapshot - инициализируем кошельки из данных трейдов
        if (newTrades.IsSnapshot)
        {
            await CreateSnapshotByTradesSnapshot(newTrades);
            return;
        }

        // Обрабатываем обычные трейды
        foreach (var trade in newTrades.Trades)
        {
            // Пропускаем не-фьючерсные трейды
            if (!trade.IsFuture)
            {
                continue;
            }

            try
            {
                await AddTrade(trade);
            }
            catch (Exception ex)
            {
                _logger.LogError($"CurrentWalletPositionService.OnNewTrades: TradeId={trade.TradeId} Ошибка обработки трейда: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Инициализирует snapshot для кошелька из готового объекта
    /// </summary>
    public void InitializeWalletSnapshot(WalletPositionsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot, nameof(snapshot));

        if (_walletPositionSnapshot.ContainsKey(snapshot.Wallet)) return;

        if (!_walletPositionSnapshot.TryAdd(snapshot.Wallet, snapshot))
        {
            _logger.LogError($"CurrentWalletPositionService.InitializeWalletSnapshot: Wallet={snapshot.Wallet} Не получилось инициализировать Snapshot");
        }
    }

    /// <summary>
    /// Очищает все snapshots и семафоры
    /// Используется для: тестирования, управления памятью в runtime, полного сброса состояния
    /// </summary>
    public void ClearAllSnapshots()
    {
        var snapshotCount = _walletPositionSnapshot.Count;
        var semaphoreCount = _walletSemaphores.Count;

        // Освобождаем все семафоры перед удалением
        foreach (var kvp in _walletSemaphores)
        {
            try
            {
                kvp.Value?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"CurrentWalletPositionService.ClearAllSnapshots: Wallet={kvp.Key} Ошибка при освобождении семафора");
            }
        }

        _walletPositionSnapshot.Clear();
        _walletSemaphores.Clear();

        _logger.LogInformation($"CurrentWalletPositionService.ClearAllSnapshots: Все snapshots и семафоры очищены (было snapshots: {snapshotCount}, семафоров: {semaphoreCount})");
    }

    /// <summary>
    /// Рассчитывает суммарное количество из pending ордеров (без учета реальной позиции)
    ///
    /// Логика фильтрации pending ордеров по ЦЕНЕ исполнения:
    /// - Для Long ордера at price P (исполнится при падении цены до P):
    ///   Учитываются только Long ордера с price > P (они исполнятся раньше при движении цены вниз)
    /// - Для Short ордера at price P (исполнится при росте цены до P):
    ///   Учитываются ВСЕ Long ордера (могли исполниться до роста) + Short ордера с price < P (исполнятся раньше при росте)
    /// </summary>
    /// <param name="order">Ордер, для которого рассчитывается потенциальная позиция (будет исключен из расчета)</param>
    /// <returns>Суммарное количество из pending ордеров (со знаком: Long = положительное, Short = отрицательное)</returns>
    public decimal CalculatePendingOrdersQuantity(OriginalOrder order)
    {
        // Получаем pending ордера для данного кошелька и символа
        var pendingOrderFills = _fillsOrderService.GetPendingOrdersByWalletAndSymbol(order.Wallet, order.Symbol);

        // Суммируем pending ордера с фильтрацией по цене в одном проходе
        decimal pendingQuantity = 0;
        int totalPendingCount = pendingOrderFills.Length;
        int consideredCount = 0;

        foreach (var orderFills in pendingOrderFills)
        {
            var pendingOrder = orderFills.OriginalOrder;

            // Исключаем сам ордер
            if (pendingOrder.OrderId == order.OrderId)
                continue;

            // Фильтруем по направлению и цене в зависимости от текущего ордера
            bool shouldConsider = order.Direction == Direction.Short
                ? pendingOrder.Direction == Direction.Long || pendingOrder.Price < order.Price
                : pendingOrder.Direction == Direction.Long && pendingOrder.Price > order.Price;

            if (shouldConsider)
            {
                decimal orderQuantity = pendingOrder.Direction == Direction.Long
                    ? pendingOrder.Quantity
                    : -pendingOrder.Quantity;
                pendingQuantity += orderQuantity;
                consideredCount++;
            }
        }

        _logger.LogDebug(
            $"CurrentWalletPositionService.CalculatePendingOrdersQuantity: " +
            $"Wallet={order.Wallet} Symbol={order.Symbol} OrderId={order.OrderId} " +
            $"Direction={order.Direction} Price={order.Price} " +
            $"PendingQuantity={pendingQuantity} ({consideredCount}/{totalPendingCount} orders considered)");

        return pendingQuantity;
    }

    /// <summary>
    /// Рассчитывает потенциальную позицию для указанного символа и кошелька
    /// Потенциальная позиция = реальная позиция + все открытые pending ордера (кроме указанного)
    /// Используется для определения SubType ордера
    /// </summary>
    public decimal CalculatePotentialPosition(OriginalOrder order)
    {
        // Получаем реальную позицию
        var realPositions = GetPositionsFromSnapshot(order.Wallet, order.Symbol);
        decimal realQuantity = realPositions.Length > 0 ? realPositions[0].Quantity : 0;

        // Рассчитываем количество из pending ордеров
        decimal pendingQuantity = CalculatePendingOrdersQuantity(order);

        decimal potentialPosition = realQuantity + pendingQuantity;

        _logger.LogDebug(
            $"CurrentWalletPositionService.CalculatePotentialPosition: " +
            $"Wallet={order.Wallet} Symbol={order.Symbol} OrderId={order.OrderId} " +
            $"Real={realQuantity} + Pending={pendingQuantity} = Potential={potentialPosition}");

        return potentialPosition;
    }

    public async Task<OrderSubType> AddTrade(OriginalTrade trade)
    {
        ArgumentNullException.ThrowIfNull(trade, nameof(trade));

        if (!_walletPositionSnapshot.ContainsKey(trade.Wallet))
        {
            _logger.LogError($"CurrentWalletPositionService.AddTrade: TradeId={trade.TradeId} Wallet={trade.Wallet} Snapshot не инициализирован");
            return OrderSubType.None;
        }

        var semaphore = _walletSemaphores.GetOrAdd(trade.Wallet, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var openPos = GetPositionsFromSnapshot(trade.Wallet, trade.Symbol);

            // Нет открытых позиций - открываем новую
            if (openPos.Length == 0)
            {
                return await HandleOpenPosition(trade);
            }

            // Одна открытая позиция - обрабатываем
            if (openPos.Length == 1)
            {
                return await ProcessTradeWithPosition(openPos[0], trade);
            }

            // Больше одной позиции - ошибка
            if (openPos.Length > 1)
            {
                _logger.LogError($"CurrentWalletPositionService.AddTrade: TradeId={trade.TradeId} Wallet={trade.Wallet} Symbol={trade.Symbol} Обнаружено {openPos.Length} открытых позиций (разнонаправленные)");
                return OrderSubType.None;
            }

            return OrderSubType.None;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Пытаемся получить snapshot по кошельку из MemoryCache , если нет, то запрашиваем у провайдера
    /// </summary>
    public async Task<WalletPositionsSnapshot> GetSnapshot(Wallet wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet, nameof(wallet));

        // Быстрая проверка без блокировки
        if (_walletPositionSnapshot.TryGetValue(wallet, out WalletPositionsSnapshot snapshot))
        {
            return snapshot;
        }

        // Получаем семафор для этого кошелька (создаем если нет)
        var semaphore = _walletSemaphores.GetOrAdd(wallet, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            // Double-check: проверяем снова после получения блокировки
            // (другой поток мог добавить пока мы ждали)
            if (_walletPositionSnapshot.TryGetValue(wallet, out snapshot))
            {
                return snapshot;
            }

            // Теперь точно нужно создать snapshot
            var walletInfo = await _walletInfoProvider.GetInfo(wallet);
            var walletSnapshot = walletInfo.ToWalletSnapshot();

            // Добавляем в dictionary
            if (_walletPositionSnapshot.TryAdd(wallet, walletSnapshot))
            {
                _logger.LogInformation($"CurrentWalletPositionService.GetSnapshot: Wallet={wallet} Создан новый snapshot");
                return walletSnapshot;
            }

            // Если TryAdd вернул false (другой поток успел добавить между проверкой и добавлением)
            // возвращаем ту версию что в dictionary
            _logger.LogWarning($"CurrentWalletPositionService.GetSnapshot: Wallet={wallet} Snapshot уже был добавлен другим потоком");
            return _walletPositionSnapshot[wallet];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public OrderSubType GetOrderSubType(OriginalOrder order)
    {
        ArgumentNullException.ThrowIfNull(order, nameof(order));

        if (!_walletPositionSnapshot.TryGetValue(order.Wallet, out var snapshot))
        {
            _logger.LogError($"CurrentWalletPositionService.GetOrderSubType: OrderId={order.OrderId} Wallet={order.Wallet} walletPositionSnapshot не инициализирован - возвращаем OrderSubType.None");
            return OrderSubType.None;
        }

        // Рассчитываем потенциальную позицию (реальная + все открытые pending ордера, кроме текущего)
        // С учетом Direction и Price текущего ордера для правильной фильтрации pending ордеров
        decimal potentialPosition = CalculatePotentialPosition(order);

        // Нет ни реальной позиции, ни pending ордеров - это Open
        if (potentialPosition == 0)
        {
            _logger.LogInformation($"CurrentWalletPositionService.GetOrderSubType: OrderId={order.OrderId} Wallet={order.Wallet} Symbol={order.Symbol} Нет позиций (реальных и pending) - возвращаем OrderSubType.Open");
            return OrderSubType.Open;
        }

        // Есть позиция (реальная или потенциальная) - определяем тип
        return DetermineOrderSubType(potentialPosition, order);
    }

    /// <summary>
    /// Получаем из snapshot текущее значение плеча для кошелька и символа
    /// </summary>
    public async Task<int?> TryGetLeverage(Wallet wallet, string symbol)
    {
        ArgumentNullException.ThrowIfNull(wallet, nameof(wallet));
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));

        var snapshot = await GetSnapshot(wallet);
        var position = snapshot.Positions.FirstOrDefault(p => p.Symbol == symbol);

        if(position is null) return null;

        return position.Leverage;
    }

    /// <summary>
    /// Пересчитать SubType для всех pending оригинальных ордеров по указанному кошельку и символу
    /// Вызывается при изменении статуса любого ордера для обновления SubType остальных pending ордеров
    /// </summary>
    public void RecalculateSubTypesForSymbol(Wallet wallet, string symbol)
    {
        ArgumentNullException.ThrowIfNull(wallet, nameof(wallet));
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));

        try
        {
            // Получаем все pending оригинальные ордера для данного кошелька и символа из FillsOrderService
            var pendingOrderFills = _fillsOrderService.GetPendingOrdersByWalletAndSymbol(wallet, symbol);

            if (pendingOrderFills.Length == 0)
            {
                _logger.LogDebug($"CurrentWalletPositionService.RecalculateSubTypesForSymbol: Wallet={wallet} Symbol={symbol} Нет pending ордеров");
                return;
            }

            _logger.LogInformation($"CurrentWalletPositionService.RecalculateSubTypesForSymbol: Wallet={wallet} Symbol={symbol} Пересчет SubType для {pendingOrderFills.Length} pending ордеров");

            int updatedCount = 0;

            // Пересчитываем SubType для каждого pending OriginalOrder
            foreach (var orderFills in pendingOrderFills)
            {
                var originalOrder = orderFills.OriginalOrder;
                var oldSubType = originalOrder.SubType;
                var newSubType = GetOrderSubType(originalOrder);

                // Обновляем только если SubType изменился
                if (newSubType != oldSubType)
                {
                    _fillsOrderService.UpdateOrderSubType(originalOrder.OrderId, newSubType);
                    updatedCount++;

                    _logger.LogInformation($"CurrentWalletPositionService.RecalculateSubTypesForSymbol: OrderId={originalOrder.OrderId} OrderSubType изменен: {oldSubType} -> {newSubType}");
                }
            }

            _logger.LogInformation($"CurrentWalletPositionService.RecalculateSubTypesForSymbol: Wallet={wallet} Symbol={symbol} Пересчет завершен. Обновлено {updatedCount} из {pendingOrderFills.Length} ордеров");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"CurrentWalletPositionService.RecalculateSubTypesForSymbol: Wallet={wallet} Symbol={symbol} Ошибка при пересчете SubType");
        }
    }

    /// <summary>
    /// Получить все отслеживаемые кошельки
    /// </summary>
    public IEnumerable<Wallet> GetAllWallets()
    {
        return [.. _walletPositionSnapshot.Keys];
    }

    #region Private Event Handlers

    private async Task CreateSnapshotByTradesSnapshot((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        var firstFutureTrade = newTrades.Trades.FirstOrDefault(t => t.IsFuture);

        if (firstFutureTrade == null)
        {
            _logger.LogError("CurrentWalletPositionService.CreateSnapshotByTradesSnapshot: Не найдено фьючерсных трейдов");
            return;
        }

        try
        {
            var wallet = firstFutureTrade.Wallet;

            var walletInfo = await _walletInfoProvider.GetInfo(wallet, false);
            var walletSnapshot = walletInfo.ToWalletSnapshot();

            InitializeWalletSnapshot(walletSnapshot);

            _logger.LogInformation($"CurrentWalletPositionService.CreateSnapshotByTradesSnapshot: Wallet={wallet} Инициализирован snapshot с {walletSnapshot.Positions.Count} позициями");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CurrentWalletPositionService.CreateSnapshotByTradesSnapshot: Wallet={firstFutureTrade.Wallet} Ошибка инициализации snapshot: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Получает позицию из snapshot
    /// </summary>
    private Position[] GetPositionsFromSnapshot(Wallet wallet, string symbol)
    {
        var snapshot = _walletPositionSnapshot[wallet];
        var positions = snapshot.Positions;

        // Быстрая проверка - если нет позиций вообще
        if (positions.Count == 0)
            return Array.Empty<Position>();

        // Ищем позиции с указанным символом
        List<Position>? result = null;
        foreach (var position in positions)
        {
            if (position.Symbol == symbol)
            {
                result ??= new List<Position>(1); // Обычно будет только одна позиция
                result.Add(position);
            }
        }

        return result?.ToArray() ?? Array.Empty<Position>();
    }

    /// <summary>
    /// Обновляет позицию при увеличении (Increase)
    /// </summary>
    private void UpdatePositionForIncrease(Position position, OriginalTrade trade)
    {
        var newQuantity = position.Quantity + trade.RealQuantity;
        var newVolumeUsd = position.VolumeUsd + trade.VolumeUsd;

        // Рассчитываем среднюю цену входа, проверяя деление на ноль
        if (newQuantity != 0)
        {
            position.AverageEntryPrice = newVolumeUsd / newQuantity;
        }
        else
        {
            _logger.LogError($"CurrentWalletPositionService.UpdatePositionForIncrease: Symbol={trade.Symbol} Деление на ноль при расчете AverageEntryPrice! NewQuantity={newQuantity}");
            position.AverageEntryPrice = 0;
        }

        position.Quantity = newQuantity;
        position.VolumeUsd = newVolumeUsd;

        _logger.LogInformation($"CurrentWalletPositionService.UpdatePositionForIncrease: Symbol={trade.Symbol} Позиция обновлена, Quantity={newQuantity}, VolumeUsd={newVolumeUsd}");
    }

    /// <summary>
    /// Обновляет позицию при уменьшении (Decrease)
    /// </summary>
    private void UpdatePositionForDecrease(Position position, OriginalTrade trade)
    {
        var newQuantity = position.Quantity + trade.RealQuantity;
        var newVolumeUsd = position.VolumeUsd - trade.VolumeUsd;

        // Рассчитываем среднюю цену входа, проверяя деление на ноль
        if (newQuantity != 0)
        {
            position.AverageEntryPrice = newVolumeUsd / newQuantity;
        }
        else
        {
            _logger.LogError($"CurrentWalletPositionService.UpdatePositionForDecrease: Symbol={trade.Symbol} Деление на ноль при расчете AverageEntryPrice! NewQuantity={newQuantity}, NewVolume={newVolumeUsd}");
            position.AverageEntryPrice = 0;
        }

        position.Quantity = newQuantity;
        position.VolumeUsd = newVolumeUsd;

        _logger.LogInformation($"CurrentWalletPositionService.UpdatePositionForDecrease: Symbol={trade.Symbol} Позиция уменьшена, Quantity={newQuantity}, VolumeUsd={newVolumeUsd}");
    }

    /// <summary>
    /// Удаляет позицию из snapshot
    /// </summary>
    private void RemovePositionFromSnapshot(Wallet wallet, Position position)
    {
        _walletPositionSnapshot[wallet].Positions.Remove(position);
        _logger.LogInformation($"CurrentWalletPositionService.RemovePositionFromSnapshot: Wallet={wallet} Symbol={position.Symbol} Позиция удалена из snapshot");
    }

    /// <summary>
    /// Добавляет позицию в snapshot из провайдера
    /// </summary>
    private async Task<Position?> AddPositionToSnapshotFromServer(Wallet wallet, string symbol)
    {
        var walletInfo = await _walletInfoProvider.GetInfo(wallet, false);

        if (!walletInfo.Positions.TryGetValue(symbol, out var position))
        {
            _logger.LogError($"CurrentWalletPositionService.AddPositionToSnapshotFromServer: Wallet={wallet} Symbol={symbol} Не удалось найти позицию");
            return null;
        }

        _walletPositionSnapshot[wallet].Positions.Add(position);
        _logger.LogInformation($"CurrentWalletPositionService.AddPositionToSnapshotFromServer: Wallet={wallet} Symbol={symbol} Позиция добавлена в snapshot");

        return position;
    }

    /// <summary>
    /// Определяет тип операции на основе потенциальной позиции (реальная + pending ордера) и нового ордера
    /// </summary>
    /// <param name="potentialPosition">Потенциальная позиция (положительная = Long, отрицательная = Short)</param>
    /// <param name="order">Новый ордер</param>
    private OrderSubType DetermineOrderSubType(decimal potentialPosition, OriginalOrder order)
    {
        // Определяем направление потенциальной позиции
        Direction positionDirection = potentialPosition > 0 ? Direction.Long : Direction.Short;

        // Одинаковое направление = INCREASE
        if (positionDirection == order.Direction)
        {
            _logger.LogInformation($"CurrentWalletPositionService.DetermineOrderSubType: Symbol={order.Symbol} Потенциальная позиция ({potentialPosition}) в том же направлении {order.Direction} - возвращаем OrderSubType.Increase");
            return OrderSubType.Increase;
        }

        // Противоположное направление - определяем Close, Decrease или Flip
        // Рассчитываем новую позицию после исполнения ордера
        decimal newQuantity = potentialPosition + order.RealQuantity;

        // Проверяем на полное закрытие (точное 0 или очень близко к 0)
        if (Math.Abs(newQuantity) < 0.001M)
        {
            _logger.LogInformation($"CurrentWalletPositionService.DetermineOrderSubType: Symbol={order.Symbol} Потенциальная позиция ({potentialPosition}) будет полностью закрыта ордером {order.Quantity}, новая позиция {newQuantity} - возвращаем OrderSubType.Close");
            return OrderSubType.Close;
        }

        // Проверяем знак новой позиции
        if (Math.Sign(newQuantity) == Math.Sign(potentialPosition))
        {
            // Знак не изменился = частичное закрытие
            _logger.LogInformation($"CurrentWalletPositionService.DetermineOrderSubType: Symbol={order.Symbol} Потенциальная позиция ({potentialPosition}) будет частично закрыта ордером {order.Quantity}, новая позиция {newQuantity} - возвращаем OrderSubType.Decrease");
            return OrderSubType.Decrease;
        }

        // Знак изменился = переворот позиции (Flip)
        _logger.LogWarning($"CurrentWalletPositionService.DetermineOrderSubType: OrderId={order.OrderId} Symbol={order.Symbol} Потенциальная позиция ({potentialPosition}) будет перевернута ордером {order.RealQuantity}, новая позиция {newQuantity} - возвращаем OrderSubType.Flip");
        return OrderSubType.Flip;
    }

    #endregion

    #region Position Operation Handlers

    /// <summary>
    /// Обрабатывает открытие новой позиции (Open)
    /// </summary>
    private async Task<OrderSubType> HandleOpenPosition(OriginalTrade trade)
    {
        var position = await AddPositionToSnapshotFromServer(trade.Wallet, trade.Symbol);

        if (position == null)
        {
            return OrderSubType.None;
        }

        _logger.LogInformation($"CurrentWalletPositionService.HandleOpenPosition: Wallet={trade.Wallet} Symbol={trade.Symbol} Открыта новая позиция");
        return OrderSubType.Open;
    }

    /// <summary>
    /// Обрабатывает увеличение существующей позиции (Increase)
    /// </summary>
    private OrderSubType HandleIncreasePosition(Position position, OriginalTrade trade)
    {
        _logger.LogInformation($"CurrentWalletPositionService.HandleIncreasePosition: Symbol={trade.Symbol} Direction={trade.Direction} Увеличение позиции");

        UpdatePositionForIncrease(position, trade);

        return OrderSubType.Increase;
    }

    /// <summary>
    /// Обрабатывает уменьшение существующей позиции (Decrease)
    /// </summary>
    private OrderSubType HandleDecreasePosition(Position position, OriginalTrade trade)
    {
        _logger.LogInformation($"CurrentWalletPositionService.HandleDecreasePosition: Symbol={trade.Symbol} Direction={trade.Direction} Уменьшение позиции");

        UpdatePositionForDecrease(position, trade);

        return OrderSubType.Decrease;
    }

    /// <summary>
    /// Обрабатывает полное закрытие позиции (Close)
    /// </summary>
    private OrderSubType HandleClosePosition(Wallet wallet, Position position, OriginalTrade trade)
    {
        _logger.LogInformation($"CurrentWalletPositionService.HandleClosePosition: Wallet={wallet} Symbol={trade.Symbol} Полное закрытие позиции");

        RemovePositionFromSnapshot(wallet, position);

        return OrderSubType.Close;
    }

    /// <summary>
    /// Обрабатывает переворот позиции (Close текущей + Open противоположной)
    /// </summary>
    private async Task<OrderSubType> HandleFlipPosition(Wallet wallet, Position position, OriginalTrade trade)
    {
        _logger.LogWarning($"CurrentWalletPositionService.HandleFlipPosition: Wallet={wallet} Symbol={trade.Symbol} ПЕРЕВОРОТ позиции! Было: {position.Direction} {position.Quantity}, Trade: {trade.Direction} {trade.RealQuantity}");

        // Удаляем старую позицию
        RemovePositionFromSnapshot(wallet, position);

        // Запрашиваем новую позицию у провайдера
        var newPosition = await AddPositionToSnapshotFromServer(wallet, trade.Symbol);

        if (newPosition != null)
        {
            _logger.LogInformation($"CurrentWalletPositionService.HandleFlipPosition: Wallet={wallet} Symbol={trade.Symbol} После переворота добавлена новая позиция: {newPosition}");
        }

        // Возвращаем Close (так как текущая позиция закрылась)
        return OrderSubType.Close;
    }

    /// <summary>
    /// Определяет тип операции и направляет обработку в соответствующий handler
    /// </summary>
    private async Task<OrderSubType> ProcessTradeWithPosition(Position position, OriginalTrade trade)
    {
        // Одинаковое направление = INCREASE
        if (position.Direction == trade.Direction)
        {
            return HandleIncreasePosition(position, trade);
        }

        // Противоположное направление - определяем Close, Decrease или Flip
        var newQuantity = position.Quantity + trade.RealQuantity;

        if (newQuantity == 0)
        {
            // Полное закрытие
            return HandleClosePosition(trade.Wallet, position, trade);
        }

        if (Math.Sign(position.Quantity) == Math.Sign(newQuantity))
        {
            // Частичное закрытие (направление не изменилось)
            return HandleDecreasePosition(position, trade);
        }
        else
        {
            // Переворот позиции
            return await HandleFlipPosition(trade.Wallet, position, trade);
        }
    }

    #endregion
}