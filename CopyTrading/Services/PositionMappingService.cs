using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using System.Collections.Concurrent;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для управления маппингами между позициями трейдера и копируемыми позициями
/// Хранит данные в памяти (in-memory)
/// </summary>
public class PositionMappingService(ILogger<PositionMappingService> _logger)
{
    private readonly ConcurrentDictionary<string, PositionMapping> _mappings = new();

    public int GetMappingsCount => _mappings.Count;

    /// <summary>
    /// Получить маппинг по параметрам
    /// </summary>
    public PositionMapping? GetMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);
        _mappings.TryGetValue(key, out var mapping);
        return mapping;
    }

    /// <summary>
    /// Сохранить или обновить маппинг
    /// </summary>
    public void SaveOrUpdateMapping(PositionMapping mapping)
    {
        mapping.LastUpdate = DateTime.Now;
        var key = mapping.GetKey();

        _mappings.AddOrUpdate(key, mapping, (k, old) => mapping);

        _logger.LogInformation($"Маппинг сохранен: {mapping}");
    }

    /// <summary>
    /// Удалить маппинг (при полном закрытии позиции)
    /// </summary>
    public bool DeleteMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);
        var removed = _mappings.TryRemove(key, out var mapping);

        if (removed)
        {
            _logger.LogInformation($"Маппинг удален: {mapping}");
        }
        else
        {
            _logger.LogWarning($"Не удалось удалить маппинг: {key}");
        }

        return removed;
    }

    /// <summary>
    /// Обновить количество в вашей позиции
    /// </summary>
    public bool UpdateMyQuantity(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction, decimal newQuantity)
    {
        var mapping = GetMapping(traderWallet, myWallet, symbol, direction);
        if (mapping == null)
        {
            _logger.LogWarning($"Маппинг не найден для обновления MyQuantity: {symbol} {direction}");
            return false;
        }

        mapping.MyQuantity = newQuantity;
        SaveOrUpdateMapping(mapping);
        return true;
    }

    /// <summary>
    /// Получить все маппинги для конкретного трейдера
    /// </summary>
    public IEnumerable<PositionMapping> GetMappingsByTrader(Wallet traderWallet, Wallet myWallet)
    {
        return [.. _mappings.Values.Where(m => m.TraderWallet.Equals(traderWallet) && m.MyWallet.Equals(myWallet))];
    }

    /// <summary>
    /// Получить все активные маппинги
    /// </summary>
    public IEnumerable<PositionMapping> GetAllMappings()
    {
        return [.. _mappings.Values];
    }

    /// <summary>
    /// Очистить все маппинги (используется для тестирования)
    /// </summary>
    public void ClearAllMappings()
    {
        var count = _mappings.Count;
        _mappings.Clear();
        _logger.LogInformation($"Все маппинги очищены (было {count})");
    }
}
