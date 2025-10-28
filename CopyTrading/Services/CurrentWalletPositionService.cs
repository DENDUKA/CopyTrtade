using CopyTrading.DataEvents;
using CopyTrading.Mappers;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

public class CurrentWalletPositionService
{
    private readonly IWalletInfoProvider _walletInfoProvider;
    private readonly ILogger<CurrentWalletPositionService> _logger;
    private readonly ConcurrentDictionary<Wallet, WalletPositionsSnapshot> _walletPositionSnapshot = [];
    private readonly ConcurrentDictionary<Wallet, SemaphoreSlim> _walletSemaphores = [];

    public CurrentWalletPositionService(
        IWalletInfoProvider walletInfoProvider,
        ILogger<CurrentWalletPositionService> logger)
    {
        _walletInfoProvider = walletInfoProvider;
        _logger = logger;

        // Подписываемся на события новых трейдов
        DataBusEvents.NewTrades += OnNewTrades;
    }

    private async void OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        // Обрабатываем каждый трейд
        foreach (var trade in newTrades.Trades)
        {
            // Пропускаем трейды из snapshot (они используются только для инициализации)
            if (newTrades.IsSnapshot)
            {
                continue;
            }

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
                _logger.LogError($"CurrentWalletPositionService OnNewTrades ошибка обработки трейда {trade.TradeId}: {ex.Message}");
            }
        }
    }

    public void InitializeWalletSnapshot(WalletPositionsSnapshot snapshot)
    {
        if (_walletPositionSnapshot.ContainsKey(snapshot.Wallet)) return;

        if (!_walletPositionSnapshot.TryAdd(snapshot.Wallet, snapshot))
        {
            _logger.LogError($"CurrentWalletPositionService Initialize не получилось инициализировать Snapshot для {snapshot.Wallet}");
        }
    }

    public async Task<OrderSubType> AddTrade(OriginalTrade trade)
    {
        if (!_walletPositionSnapshot.ContainsKey(trade.Wallet))
        {
            _logger.LogError($"CurrentWalletPositionService Snapshot для {trade.Wallet} не инициализирован");
        }

        var semaphore = _walletSemaphores.GetOrAdd(trade.Wallet, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            var openPos = _walletPositionSnapshot[trade.Wallet].Positions.Where(p => p.Symbol == trade.Symbol).ToArray();

            if (openPos.Length == 0)
            {
                var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet, false);

                if (!walletInfo.Positions.TryGetValue(trade.Symbol, out var position))
                {
                    _logger.LogError($"CurrentWalletPositionService AddTrade не удалось найти позицию {trade.Symbol} у кошелька {trade.Wallet}");
                    return OrderSubType.None;
                }

                _walletPositionSnapshot[trade.Wallet].Positions.Add(position);

                return OrderSubType.Open;
            }
            if (openPos.Length == 1)
            {
                if (openPos[0].Direction == trade.Direction)
                {
                    // INCREASE: Увеличение позиции в том же направлении

                    // Рассчитываем новое среднее
                    var newQuantity = openPos[0].Quantity + trade.RealQuantity;
                    var newVolumeUsd = openPos[0].VolumeUsd + trade.VolumeUsd;

                    try
                    {
                        openPos[0].AverageEntryPrice = newVolumeUsd / newQuantity;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"CurrentWalletPositionService AddTrade Деление на ноль при расчете AverageEntryPrice! Quantity: {newQuantity}, Volume: {newVolumeUsd}");
                    }

                    // ✅ ОБНОВЛЯЕМ И Quantity И VolumeUsd!
                    openPos[0].Quantity = newQuantity;
                    openPos[0].VolumeUsd = newVolumeUsd;

                    return OrderSubType.Increase;
                }
                else
                {
                    // Противоположное направление - закрытие или уменьшение
                    var newQuantity = openPos[0].Quantity + trade.RealQuantity;

                    if (newQuantity == 0)
                    {
                        // CLOSE: Полное закрытие позиции
                        _walletPositionSnapshot[trade.Wallet].Positions.Remove(openPos[0]);
                        return OrderSubType.Close;
                    }

                    if (Math.Sign(openPos[0].Quantity) == Math.Sign(newQuantity))
                    {
                        // DECREASE: Частичное закрытие (направление не изменилось)
                        var newVolumeUsd = openPos[0].VolumeUsd - trade.VolumeUsd;

                        try
                        {
                            openPos[0].AverageEntryPrice = newVolumeUsd / newQuantity;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"CurrentWalletPositionService AddTrade Деление на ноль при Decrease! Quantity: {newQuantity}, Volume: {newVolumeUsd}");
                        }

                        // ✅ ОБНОВЛЯЕМ И Quantity И VolumeUsd!
                        openPos[0].Quantity = newQuantity;
                        openPos[0].VolumeUsd = newVolumeUsd;

                        return OrderSubType.Decrease;
                    }
                    else
                    {
                        // FLIP: Переворот позиции (Close + Open в противоположном направлении)
                        // Например: SHORT -0.3 + LONG 0.5 = LONG +0.2
                        _logger.LogWarning($"CurrentWalletPositionService AddTrade ПЕРЕВОРОТ позиции {trade.Symbol} у {trade.Wallet}! " +
                                         $"Было: {openPos[0].Direction} {openPos[0].Quantity}, " +
                                         $"Trade: {trade.Direction} {trade.RealQuantity}, " +
                                         $"Результат: {newQuantity}");

                        // Для переворота нужно:
                        // 1. Удалить старую позицию
                        _walletPositionSnapshot[trade.Wallet].Positions.Remove(openPos[0]);

                        // 2. Запросить новую позицию у провайдера
                        var walletInfo = await _walletInfoProvider.GetInfo(trade.Wallet, false);
                        if (walletInfo.Positions.TryGetValue(trade.Symbol, out var newPosition))
                        {
                            _walletPositionSnapshot[trade.Wallet].Positions.Add(newPosition);
                            _logger.LogInformation($"CurrentWalletPositionService AddTrade После переворота добавлена новая позиция: {newPosition}");
                        }

                        // Возвращаем Close (так как текущая позиция закрылась)
                        return OrderSubType.Close;
                    }
                }
            }
            if (openPos.Length > 1)
            {
                _logger.LogError($"CurrentWalletPositionService AddTrade обнаружено две открытые позиции (разнонаправленные) для {trade.Symbol} у кошелька {trade.Wallet}");
                return OrderSubType.None;
            }
        }
        finally
        {
            semaphore.Release();
        }

        return OrderSubType.None;
    }

    /// <summary>
    /// Пытаемся получить snapshot по кошельку из MemoryCache , если нет, то запрашиваем у провайдера
    /// </summary>
    /// <param name="wallet"></param>
    /// <returns></returns>
    public async Task<WalletPositionsSnapshot> GetSnapshot(Wallet wallet)
    {
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
                _logger.LogInformation($"CurrentWalletPositionService GetSnapshot создан новый snapshot для {wallet}");
                return walletSnapshot;
            }

            // Если TryAdd вернул false (другой поток успел добавить между проверкой и добавлением)
            // возвращаем ту версию что в dictionary
            _logger.LogWarning($"CurrentWalletPositionService GetSnapshot snapshot для {wallet} уже был добавлен другим потоком");
            return _walletPositionSnapshot[wallet];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<OrderSubType> GetOrderSubType(OriginalOrder order)
    {
        if (!_walletPositionSnapshot.TryGetValue(order.Wallet, out var snapshot))
        {
            _logger.LogError($"GetOrderSubType walletPositionSnapshot для {order.Wallet} не инициализирован - возвращаем OrderSubType.None");
            return OrderSubType.None;
        }

        var openPos = snapshot.Positions.Where(p => p.Symbol == order.Symbol).ToArray();

        if (openPos.Length == 0)
        {
            _logger.LogInformation($"GetOrderSubType: Нет открытых позиций для {order.Symbol} у {order.Wallet} - возвращаем OrderSubType.Open");
            return OrderSubType.Open;
        }
        if (openPos.Length == 1)
        {
            if (openPos[0].Direction == order.Direction)
            {
                _logger.LogInformation($"GetOrderSubType: Позиция {order.Symbol} в том же направлении {order.Direction} - возвращаем OrderSubType.Increase");
                return OrderSubType.Increase;
            }
            else
            {
                if (openPos[0].Quantity + order.RealQuantity == 0)
                {
                    _logger.LogInformation($"GetOrderSubType: Позиция {order.Symbol} будет полностью закрыта - возвращаем OrderSubType.Close");
                    return OrderSubType.Close;
                }

                if (Math.Abs(openPos[0].Quantity) > Math.Abs(order.Quantity))
                {
                    _logger.LogInformation($"GetOrderSubType: Позиция {order.Symbol} будет частично закрыта - возвращаем OrderSubType.Decrease");
                    return OrderSubType.Decrease;
                }
                //else это закрытие текущей позиции и сразу открытие позиции в противоположную сторону (Long > Short) (Short > Long)

                _logger.LogError($"CurrentWalletPositionService GetOrderSubType не удалось уменьшить позицию {order.Symbol} у кошелька {order.Wallet} TradeVolume больше чем открытая позиция - возвращаем OrderSubType.None OrderId {order.OrderId}");
                return OrderSubType.None;
            }
        }
        if (openPos.Length > 1)
        {
            _logger.LogError($"CurrentWalletPositionService GetOrderSubType обнаружено две открытые позиции (разнонаправленные) для {order.Symbol} у кошелька {order.Wallet} - возвращаем OrderSubType.None");
            return OrderSubType.None;
        }

        return OrderSubType.None;
    }

    /// <summary>
    /// Получаем из snapshot текущее значение плеча для кошелька и символа
    /// </summary>
    public async Task<int?> TryGetLeverage(Wallet wallet, string symbol)
    {
        var snapshot = await GetSnapshot(wallet);
        var position = snapshot.Positions.FirstOrDefault(p => p.Symbol == symbol);

        if(position is null) return null;

        return position.Leverage;
    }

    /// <summary>
    /// Получить все отслеживаемые кошельки
    /// </summary>
    public IEnumerable<Wallet> GetAllWallets()
    {
        return _walletPositionSnapshot.Keys.ToList();
    }
}