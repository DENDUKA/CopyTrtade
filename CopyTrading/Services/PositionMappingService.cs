using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для управления маппингами между позициями трейдера и копируемыми позициями
/// Хранит данные в Redis для персистентности
/// </summary>
public class PositionMappingService : IPositionMappingService
{
    private readonly IRedisRepository _redisRepository;
    private readonly ILogger<PositionMappingService> _logger;

    public PositionMappingService(
        IRedisRepository redisRepository,
        ILogger<PositionMappingService> logger)
    {
        _redisRepository = redisRepository;
        _logger = logger;
    }

    public int GetMappingsCount
    {
        get
        {
            try
            {
                var mappings = _redisRepository.LoadAllPositionMappings().GetAwaiter().GetResult();
                return mappings.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get mappings count");
                return 0;
            }
        }
    }

    /// <summary>
    /// Получить маппинг по параметрам
    /// </summary>
    public PositionMapping? GetMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        try
        {
            var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);
            var mapping = _redisRepository.GetPositionMapping(key).GetAwaiter().GetResult();
            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get mapping for {Symbol} {Direction}", symbol, direction);
            return null;
        }
    }

    /// <summary>
    /// Сохранить или обновить маппинг
    /// </summary>
    public void SaveOrUpdateMapping(PositionMapping mapping)
    {
        try
        {
            mapping.LastUpdate = DateTime.Now;
            _redisRepository.SavePositionMapping(mapping).GetAwaiter().GetResult();
            _logger.LogInformation($"Маппинг сохранен: {mapping}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mapping: {Mapping}", mapping);
        }
    }

    /// <summary>
    /// Удалить маппинг (при полном закрытии позиции)
    /// </summary>
    public bool DeleteMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        try
        {
            var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);

            // Сначала получаем маппинг для логирования
            var mapping = _redisRepository.GetPositionMapping(key).GetAwaiter().GetResult();

            if (mapping == null)
            {
                _logger.LogWarning($"Не удалось удалить маппинг: {key} - не найден");
                return false;
            }

            _redisRepository.DeletePositionMapping(key).GetAwaiter().GetResult();
            _logger.LogInformation($"Маппинг удален: {mapping}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete mapping for {Symbol} {Direction}", symbol, direction);
            return false;
        }
    }

    /// <summary>
    /// Обновить количество в вашей позиции
    /// </summary>
    public bool UpdateMyQuantity(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction, decimal newQuantity)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MyQuantity for {Symbol} {Direction}", symbol, direction);
            return false;
        }
    }

    /// <summary>
    /// Получить все маппинги для конкретного трейдера
    /// </summary>
    public IEnumerable<PositionMapping> GetMappingsByTrader(Wallet traderWallet, Wallet myWallet)
    {
        try
        {
            var mappings = _redisRepository.GetMappingsByTrader(traderWallet).GetAwaiter().GetResult();
            return mappings.Where(m => m.MyWallet.Equals(myWallet));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get mappings for trader {Wallet}", traderWallet.Value);
            return Array.Empty<PositionMapping>();
        }
    }

    /// <summary>
    /// Получить все активные маппинги
    /// </summary>
    public IEnumerable<PositionMapping> GetAllMappings()
    {
        try
        {
            var mappings = _redisRepository.LoadAllPositionMappings().GetAwaiter().GetResult();
            return mappings.Values;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all mappings");
            return Array.Empty<PositionMapping>();
        }
    }

    /// <summary>
    /// Очистить все маппинги (используется для тестирования)
    /// </summary>
    public void ClearAllMappings()
    {
        try
        {
            var mappings = _redisRepository.LoadAllPositionMappings().GetAwaiter().GetResult();
            var count = mappings.Count;

            foreach (var key in mappings.Keys)
            {
                _redisRepository.DeletePositionMapping(key).GetAwaiter().GetResult();
            }

            _logger.LogInformation($"Все маппинги очищены (было {count})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all mappings");
        }
    }
}
