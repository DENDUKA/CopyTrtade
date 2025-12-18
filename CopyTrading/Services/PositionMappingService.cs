using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;
using System.Threading.Tasks;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для управления маппингами между позициями трейдера и копируемыми позициями
/// Хранит данные в Redis для персистентности
/// </summary>
public class PositionMappingService(
    IRedisRepository redisRepository,
    ILogger<PositionMappingService> logger) : IPositionMappingService
{
    public int GetMappingsCount
    {
        get
        {
            try
            {
                var mappings = redisRepository.LoadAllPositionMappings().GetAwaiter().GetResult();
                return mappings.Count;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get mappings count");
                return 0;
            }
        }
    }

    /// <summary>
    /// Получить маппинг по параметрам
    /// </summary>
    public async Task<PositionMapping?> GetMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        try
        {
            var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);
            var mapping = await redisRepository.GetPositionMapping(key);
            return mapping;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get mapping for {Symbol} {Direction}", symbol, direction);
            return null;
        }
    }

    /// <summary>
    /// Сохранить или обновить маппинг
    /// </summary>
    public async Task SaveOrUpdateMapping(PositionMapping mapping)
    {
        try
        {
            mapping.LastUpdate = DateTime.Now;
            await redisRepository.SavePositionMapping(mapping);
            logger.LogInformation($"Маппинг сохранен: {mapping}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save mapping: {Mapping}", mapping);
        }
    }

    /// <summary>
    /// Удалить маппинг (при полном закрытии позиции)
    /// </summary>
    public async Task<bool> DeleteMapping(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction)
    {
        try
        {
            var key = PositionMapping.CreateKey(traderWallet, myWallet, symbol, direction);

            // Сначала получаем маппинг для логирования
            var mapping = await redisRepository.GetPositionMapping(key);

            if (mapping == null)
            {
                logger.LogWarning($"Не удалось удалить маппинг: {key} - не найден");
                return false;
            }

            await redisRepository.DeletePositionMapping(key);
            logger.LogInformation($"Маппинг удален: {mapping}");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete mapping for {Symbol} {Direction}", symbol, direction);
            return false;
        }
    }

    /// <summary>
    /// Обновить количество в вашей позиции
    /// </summary>
    public async Task<bool> UpdateMyQuantity(Wallet traderWallet, Wallet myWallet, string symbol, Direction direction, decimal newQuantity)
    {
        try
        {
            var mapping = await GetMapping(traderWallet, myWallet, symbol, direction);
            if (mapping == null)
            {
                logger.LogWarning($"Маппинг не найден для обновления MyQuantity: {symbol} {direction}");
                return false;
            }

            mapping.MyQuantity = newQuantity;
            SaveOrUpdateMapping(mapping);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update MyQuantity for {Symbol} {Direction}", symbol, direction);
            return false;
        }
    }

    /// <summary>
    /// Получить все маппинги для конкретного трейдера
    /// </summary>
    public async Task<IEnumerable<PositionMapping>> GetMappingsByTrader(Wallet traderWallet, Wallet myWallet)
    {
        try
        {
            var mappings = await redisRepository.GetMappingsByTrader(traderWallet);
            return mappings.Where(m => m.MyWallet.Equals(myWallet));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get mappings for trader {Wallet}", traderWallet.Value);
            return Array.Empty<PositionMapping>();
        }
    }

    /// <summary>
    /// Получить все активные маппинги
    /// </summary>
    public async Task<IEnumerable<PositionMapping>> GetAllMappings()
    {
        try
        {
            var mappings = await redisRepository.LoadAllPositionMappings();
            return mappings.Values;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get all mappings");
            return Array.Empty<PositionMapping>();
        }
    }

    /// <summary>
    /// Очистить все маппинги (используется для тестирования)
    /// </summary>
    public async Task ClearAllMappings()
    {
        try
        {
            var mappings = await redisRepository.LoadAllPositionMappings();
            var count = mappings.Count;

            foreach (var key in mappings.Keys)
            {
                await redisRepository.DeletePositionMapping(key);
            }

            logger.LogInformation($"Все маппинги очищены (было {count})");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear all mappings");
        }
    }
}
