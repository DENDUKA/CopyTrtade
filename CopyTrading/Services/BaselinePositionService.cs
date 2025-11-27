using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для хранения базовых (начальных) позиций трейдера, которые мы не копируем.
/// Отслеживает "точку входа" для каждой пары (wallet, symbol).
/// </summary>
public class BaselinePositionService(
    ICurrentWalletPositionService currentWalletPositionService,
    ILogger<BaselinePositionService> logger) : IBaselinePositionService
{
    private readonly ICurrentWalletPositionService _currentWalletPositionService = currentWalletPositionService;
    private readonly ILogger<BaselinePositionService> _logger = logger;

    // Ключ: (Wallet, Symbol), Значение: базовая позиция (RealQuantity)
    private readonly ConcurrentDictionary<(Wallet Wallet, string Symbol), decimal> _baselinePositions = new();

    /// <summary>
    /// Инициализирует базовые позиции для всех отслеживаемых кошельков и символов
    /// </summary>
    public async Task Start()
    {
        _logger.LogInformation("BaselinePositionService.Start: Начинаем инициализацию базовых позиций");

        try
        {
            // Получаем все отслеживаемые кошельки
            var wallets = _currentWalletPositionService.GetAllWallets();
            int totalPositions = 0;

            foreach (var wallet in wallets)
            {
                try
                {
                    // Получаем снапшот позиций для кошелька
                    var snapshot = await _currentWalletPositionService.GetSnapshot(wallet);

                    if (snapshot?.Positions == null)
                    {
                        _logger.LogWarning($"BaselinePositionService.Start: Snapshot для кошелька {wallet.Value} пуст");
                        continue;
                    }

                    // Сохраняем все позиции как базовые
                    foreach (var position in snapshot.Positions)
                    {
                        var key = (wallet, position.Symbol);
                        _baselinePositions[key] = position.Quantity;
                        totalPositions++;

                        _logger.LogDebug(
                            $"BaselinePositionService.Start: Wallet={wallet.Value}, Symbol={position.Symbol}, " +
                            $"BaselinePosition={position.Quantity}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"BaselinePositionService.Start: Ошибка при загрузке позиций для кошелька {wallet.Value}");
                }
            }

            _logger.LogInformation(
                $"BaselinePositionService.Start: Инициализация завершена. " +
                $"Загружено {totalPositions} позиций для {wallets.Count()} кошельков");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaselinePositionService.Start: Критическая ошибка при инициализации");
            throw;
        }
    }

    /// <summary>
    /// Обновляет базовые позиции при получении новых трейдов.
    /// При уменьшении объема текущей позиции трейдера обновляет базовую позицию.
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
                // Получаем текущую позицию из снапшота
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
    /// Получает базовую позицию для указанной пары (wallet, symbol)
    /// </summary>
    public decimal GetBaselinePosition(Wallet wallet, string symbol)
    {
        var key = (wallet, symbol);
        return _baselinePositions.TryGetValue(key, out var position) ? position : 0;
    }
}
