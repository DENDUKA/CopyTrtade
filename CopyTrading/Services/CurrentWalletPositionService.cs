using CopyTrading.Mappers;
using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Models.Values;
using CopyTrading.Services.Interfaces;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

public class CurrentWalletPositionService(
    IWalletInfoProvider _walletInfoProvider,
    ILogger<CurrentWalletPositionService> _logger)
{
    private readonly ConcurrentDictionary<Wallet, WalletPositionsSnapshot> _walletPositionSnapshot = [];
    private readonly ConcurrentDictionary<Wallet, SemaphoreSlim> _walletSemaphores = [];

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
                    //TODO тут может быть деление на 0 пока хз как , openPos[0].Quantity может быть отрецительным если short , trade.Q - всегда положительный
                    try
                    {
                        openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd + trade.VolumeUsd) / (openPos[0].Quantity + trade.Quantity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"CurrentWalletPositionService AddTrade Деление на ноль !!!");
                    }
                    
                    openPos[0].Quantity += trade.RealQuantity;
                    return OrderSubType.Increase;
                }
                else
                {
                    if (openPos[0].Quantity + trade.RealQuantity == 0)
                    {
                        _walletPositionSnapshot[trade.Wallet].Positions.Remove(openPos[0]);

                        return OrderSubType.Close;
                    }

                    if (Math.Abs(openPos[0].Quantity) > Math.Abs(trade.Quantity))
                    {
                        openPos[0].AverageEntryPrice = (openPos[0].VolumeUsd - trade.VolumeUsd) / (openPos[0].Quantity - trade.Quantity);
                        openPos[0].Quantity += trade.RealQuantity;

                        return OrderSubType.Decrease;
                    }

                    _logger.LogError($"CurrentWalletPositionService AddTrade не удалось уменьшить позицию {trade.Symbol} у кошелька {trade.Wallet} TradeVolume больше чем открытая позиция orderId : {trade.OrderId}");
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
        if (_walletPositionSnapshot.TryGetValue(wallet, out WalletPositionsSnapshot snapshot))
        {
            return snapshot;
        }

        var walletInfo = await _walletInfoProvider.GetInfo(wallet);
        var walletSnapshot = walletInfo.ToWalletSnapshot();

        InitializeWalletSnapshot(walletSnapshot);
        return walletSnapshot;
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
}