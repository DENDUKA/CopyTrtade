using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для хранения базовых (начальных) позиций трейдера, которые мы не копируем.
/// Отслеживает "точку входа" для каждой пары (wallet, symbol).
/// Quantity: положительное значение = Long, отрицательное = Short.
/// </summary>
public class BaselinePositionService(
    ICurrentWalletPositionService currentWalletPositionService,
    ILogger<BaselinePositionService> logger) : IBaselinePositionService
{
    private readonly ICurrentWalletPositionService _currentWalletPositionService = currentWalletPositionService;
    private readonly ILogger<BaselinePositionService> _logger = logger;

    // Ключ: (Wallet, Symbol), Значение: базовая позиция (Quantity с знаком: положительное = Long, отрицательное = Short)
    private readonly ConcurrentDictionary<(Wallet Wallet, string Symbol), decimal> _baselinePositions = new();

    /// <summary>
    /// Инициализирует базовые позиции для всех отслеживаемых кошельков и символов
    /// </summary>
    public async Task Start()
    {
        _logger.LogInformation("BaselinePositionService.Start: Начинаем инициализацию базовых позиций");

        try
        {
            var wallets = _currentWalletPositionService.GetAllWallets();
            var totalPositions = await InitializeAllWallets(wallets);

            LogInitializationComplete(totalPositions, wallets.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaselinePositionService.Start: Критическая ошибка при инициализации");
            throw;
        }
    }

    /// <summary>
    /// Инициализирует базовые позиции для всех кошельков
    /// </summary>
    /// <param name="wallets">Список кошельков для инициализации</param>
    /// <returns>Общее количество загруженных позиций</returns>
    private async Task<int> InitializeAllWallets(IEnumerable<Wallet> wallets)
    {
        int totalPositions = 0;

        foreach (var wallet in wallets)
        {
            var positionsCount = await InitializeWalletBaselinePositions(wallet);
            totalPositions += positionsCount;
        }

        return totalPositions;
    }

    /// <summary>
    /// Инициализирует базовые позиции для одного кошелька
    /// </summary>
    /// <param name="wallet">Кошелек для инициализации</param>
    /// <returns>Количество загруженных позиций для этого кошелька</returns>
    private async Task<int> InitializeWalletBaselinePositions(Wallet wallet)
    {
        try
        {
            var snapshot = await _currentWalletPositionService.GetSnapshot(wallet);

            if (snapshot?.Positions == null)
            {
                _logger.LogWarning($"BaselinePositionService.InitializeWalletBaselinePositions: Snapshot для кошелька {wallet.Value} пуст");
                return 0;
            }

            return SavePositionsAsBaseline(wallet, snapshot.Positions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"BaselinePositionService.InitializeWalletBaselinePositions: Ошибка при загрузке позиций для кошелька {wallet.Value}");
            return 0;
        }
    }

    /// <summary>
    /// Сохраняет позиции как базовые
    /// </summary>
    /// <param name="wallet">Кошелек</param>
    /// <param name="positions">Позиции для сохранения</param>
    /// <returns>Количество сохраненных позиций</returns>
    private int SavePositionsAsBaseline(Wallet wallet, IEnumerable<Models.Models.Position> positions)
    {
        int count = 0;

        foreach (var position in positions)
        {
            var key = (wallet, position.Symbol);
            // Quantity содержит знак: положительное = Long, отрицательное = Short
            _baselinePositions[key] = position.Quantity;
            count++;

            _logger.LogDebug(
                $"BaselinePositionService.SavePositionsAsBaseline: Wallet={wallet.Value}, Symbol={position.Symbol}, " +
                $"Direction={position.Direction}, BaselinePosition={position.Quantity}");
        }

        return count;
    }

    /// <summary>
    /// Логирует завершение инициализации
    /// </summary>
    private void LogInitializationComplete(int totalPositions, int walletsCount)
    {
        _logger.LogInformation(
            $"BaselinePositionService.Start: Инициализация завершена. " +
            $"Загружено {totalPositions} позиций для {walletsCount} кошельков");
    }

    /// <summary>
    /// Обновляет базовые позиции при получении новых трейдов.
    /// При уменьшении объема текущей позиции трейдера обновляет базовую позицию.
    /// Quantity: положительное = Long, отрицательное = Short.
    /// </summary>
    public async Task OnNewTrades((OriginalTrade[] Trades, bool IsSnapshot) newTrades)
    {
        // Пропускаем снапшоты
        if (newTrades.IsSnapshot)
        {
            _logger.LogDebug("BaselinePositionService.OnNewTrades: Пропускаем snapshot");
            return;
        }

        // Группируем трейды по (Wallet, Symbol)
        var tradeGroups = newTrades.Trades
            .GroupBy(trade => (trade.Wallet, trade.Symbol))
            .ToArray();

        foreach (var group in tradeGroups)
        {
            var (wallet, symbol) = group.Key;

            try
            {
                // Получаем текущую позицию из снапшота (может быть только одна - Long ИЛИ Short)
                var snapshot = await _currentWalletPositionService.GetSnapshot(wallet);
                var currentPosition = snapshot?.Positions?.FirstOrDefault(p => p.Symbol == symbol);

                var key = (wallet, symbol);

                // Если позиции нет у трейдера - удаляем из сервиса (если была)
                if (currentPosition == null)
                {
                    if (_baselinePositions.TryRemove(key, out var removedPosition))
                    {
                        _logger.LogInformation(
                            $"BaselinePositionService.OnNewTrades: Удалена базовая позиция (позиция трейдера не найдена). " +
                            $"Wallet={wallet.Value}, Symbol={symbol}, RemovedPosition={removedPosition}");
                    }
                    continue;
                }

                var currentQuantity = currentPosition.Quantity;

                // Если текущая позиция = 0 - удаляем из сервиса
                if (currentQuantity == 0)
                {
                    if (_baselinePositions.TryRemove(key, out var removedPosition))
                    {
                        _logger.LogInformation(
                            $"BaselinePositionService.OnNewTrades: Удалена базовая позиция (позиция трейдера = 0). " +
                            $"Wallet={wallet.Value}, Symbol={symbol}, RemovedPosition={removedPosition}");
                    }
                    continue;
                }

                // Получаем базовую позицию
                if (_baselinePositions.TryGetValue(key, out var baselineQuantity))
                {
                    // Проверяем уменьшился ли объем позиции
                    var currentAbsQuantity = Math.Abs(currentQuantity);
                    var baselineAbsQuantity = Math.Abs(baselineQuantity);

                    if (currentAbsQuantity < baselineAbsQuantity)
                    {
                        // Позиция уменьшилась - обновляем базовую позицию
                        _baselinePositions[key] = currentQuantity;

                        _logger.LogInformation(
                            $"BaselinePositionService.OnNewTrades: Обновлена базовая позиция. " +
                            $"Wallet={wallet.Value}, Symbol={symbol}, " +
                            $"OldBaseline={baselineQuantity}, NewBaseline={currentQuantity}");
                    }
                    else if (Math.Sign(currentQuantity) != Math.Sign(baselineQuantity))
                    {
                        // Произошла смена направления (Long → Short или Short → Long)
                        _baselinePositions[key] = currentQuantity;

                        _logger.LogInformation(
                            $"BaselinePositionService.OnNewTrades: Создана новая базовая позиция (смена направления). " +
                            $"Wallet={wallet.Value}, Symbol={symbol}, " +
                            $"OldBaseline={baselineQuantity}, NewBaseline={currentQuantity}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"BaselinePositionService.OnNewTrades: Ошибка при обработке трейдов для Wallet={wallet.Value}, Symbol={symbol}");
            }
        }
    }

    /// <summary>
    /// Получает базовую позицию для указанной пары (wallet, symbol).
    /// Возвращаемое значение: положительное = Long, отрицательное = Short, 0 = позиция отсутствует.
    /// </summary>
    public decimal GetBaselinePosition(Wallet wallet, string symbol)
    {
        var key = (wallet, symbol);
        return _baselinePositions.TryGetValue(key, out var position) ? position : 0;
    }
}
