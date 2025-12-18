using CopyTrading.Models.Models.Enums.Order;
using CopyTrading.Models.Models.Orders;
using CopyTrading.Models.Models.Trade;
using CopyTrading.Repository.RedisInterfaces;
using CopyTrading.Services.Interfaces;

namespace CopyTrading.Repository.Redis;

/// <summary>
/// Реализация общего репозитория для работы с Redis.
/// Может быть расширен методами для работы с данными различных сервисов.
/// </summary>
public class RedisRepository : IRedisRepository
{
    private readonly IRedisCacheService _redisCache;
    private readonly ILogger<RedisRepository> _logger;

    // Redis ключи - инкапсулированы в репозитории
    private const string OrdersHashKey = "orders:all";
    private const string PendingTradesHashKey = "orders:pending_trades";
    private const string OrdersWithErrorHashKey = "orders:errors";

    public RedisRepository(
        IRedisCacheService redisCache,
        ILogger<RedisRepository> logger)
    {
        _redisCache = redisCache;
        _logger = logger;
    }

    // ========== ORDERS ==========

    public async Task SaveOrder(OrderFills orderFills)
    {
        try
        {
            var orderId = orderFills.OriginalOrder.OrderId;
            await _redisCache.HashSet(OrdersHashKey, orderId.ToString(), orderFills);
            _logger.LogDebug("Saved order {OrderId} to Redis", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save order {OrderId} to Redis", orderFills.OriginalOrder.OrderId);
        }
    }

    public async Task<OrderFills?> GetOrder(long orderId)
    {
        try
        {
            var order = await _redisCache.HashGetAsync<OrderFills>(OrdersHashKey, orderId.ToString());
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order {OrderId} from Redis", orderId);
            return null;
        }
    }

    public async Task<bool> OrderExists(long orderId)
    {
        try
        {
            return await _redisCache.HashExistsAsync(OrdersHashKey, orderId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check order existence {OrderId} in Redis", orderId);
            return false;
        }
    }

    public async Task<Dictionary<long, OrderFills>> LoadAllOrders()
    {
        try
        {
            var ordersDict = await _redisCache.HashGetAllAsync<OrderFills>(OrdersHashKey);
            var result = new Dictionary<long, OrderFills>();

            foreach (var kvp in ordersDict)
            {
                if (long.TryParse(kvp.Key, out var orderId))
                {
                    result[orderId] = kvp.Value;
                }
            }

            _logger.LogInformation("Loaded {Count} orders from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load orders from Redis");
            return new Dictionary<long, OrderFills>();
        }
    }

    public async Task<OrderFills[]> GetPendingOrdersByWalletAndSymbol(string wallet, string symbol)
    {
        try
        {
            var allOrders = await LoadAllOrders();
            var pendingOrders = allOrders.Values
                .Where(o => o.OriginalOrder.Wallet.Value == wallet
                         && o.OriginalOrder.Symbol == symbol
                         && !IsFinalStatus(o.OriginalOrder.Status))
                .OrderBy(o => o.OriginalOrder.OrderId)
                .ToArray();

            _logger.LogDebug("Found {Count} pending orders for {Wallet}/{Symbol}",
                pendingOrders.Length, wallet, symbol);
            return pendingOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending orders for {Wallet}/{Symbol}", wallet, symbol);
            return Array.Empty<OrderFills>();
        }
    }

    public async Task<bool> UpdateOrderSubType(long orderId, OrderSubType newSubType)
    {
        try
        {
            var order = await GetOrder(orderId);
            if (order == null)
            {
                _logger.LogWarning("Cannot update SubType for order {OrderId} - not found", orderId);
                return false;
            }

            // Update SubType
            order.OriginalOrder.SubType = newSubType;
            await SaveOrder(order);

            _logger.LogDebug("Updated SubType for order {OrderId} to {SubType}", orderId, newSubType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update SubType for order {OrderId}", orderId);
            return false;
        }
    }

    private static bool IsFinalStatus(OrderStatus status)
    {
        return status == OrderStatus.Filled
            || status == OrderStatus.Canceled
            || status == OrderStatus.Rejected;
    }

    public async Task DeleteOrder(long orderId)
    {
        try
        {
            await _redisCache.HashDeleteAsync(OrdersHashKey, orderId.ToString());
            _logger.LogDebug("Deleted order {OrderId} from Redis", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete order {OrderId} from Redis", orderId);
        }
    }

    public async Task DeleteOrders(IEnumerable<long> orderIds)
    {
        try
        {
            foreach (var orderId in orderIds)
            {
                await _redisCache.HashDeleteAsync(OrdersHashKey, orderId.ToString());
            }
            _logger.LogDebug("Deleted {Count} orders from Redis", orderIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete multiple orders from Redis");
        }
    }

    // ========== PENDING TRADES ==========

    public async Task SavePendingTrade(OriginalTrade trade)
    {
        try
        {
            await _redisCache.HashSet(PendingTradesHashKey, trade.TradeId.ToString(), trade);
            _logger.LogDebug("Saved pending trade {TradeId} to Redis", trade.TradeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save pending trade {TradeId} to Redis", trade.TradeId);
        }
    }

    public async Task<OriginalTrade?> GetPendingTrade(long tradeId)
    {
        try
        {
            var trade = await _redisCache.HashGetAsync<OriginalTrade>(PendingTradesHashKey, tradeId.ToString());
            return trade;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending trade {TradeId} from Redis", tradeId);
            return null;
        }
    }

    public async Task<bool> PendingTradeExists(long tradeId)
    {
        try
        {
            return await _redisCache.HashExistsAsync(PendingTradesHashKey, tradeId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check pending trade existence {TradeId} in Redis", tradeId);
            return false;
        }
    }

    public async Task<Dictionary<long, OriginalTrade>> LoadAllPendingTrades()
    {
        try
        {
            var tradesDict = await _redisCache.HashGetAllAsync<OriginalTrade>(PendingTradesHashKey);
            var result = new Dictionary<long, OriginalTrade>();

            foreach (var kvp in tradesDict)
            {
                if (long.TryParse(kvp.Key, out var tradeId))
                {
                    result[tradeId] = kvp.Value;
                }
            }

            _logger.LogInformation("Loaded {Count} pending trades from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending trades from Redis");
            return new Dictionary<long, OriginalTrade>();
        }
    }

    public async Task DeletePendingTrade(long tradeId)
    {
        try
        {
            await _redisCache.HashDeleteAsync(PendingTradesHashKey, tradeId.ToString());
            _logger.LogDebug("Deleted pending trade {TradeId} from Redis", tradeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete pending trade {TradeId} from Redis", tradeId);
        }
    }

    // ========== ORDER ERRORS ==========

    public async Task SaveOrderError(long orderId, string errorMessage)
    {
        try
        {
            await _redisCache.HashSet(OrdersWithErrorHashKey, orderId.ToString(), errorMessage);
            _logger.LogDebug("Saved order error for {OrderId} to Redis", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save order error for {OrderId} to Redis", orderId);
        }
    }

    public async Task<string?> GetOrderError(long orderId)
    {
        try
        {
            var error = await _redisCache.HashGetAsync<string>(OrdersWithErrorHashKey, orderId.ToString());
            return error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order error for {OrderId} from Redis", orderId);
            return null;
        }
    }

    public async Task<bool> OrderHasError(long orderId)
    {
        try
        {
            return await _redisCache.HashExistsAsync(OrdersWithErrorHashKey, orderId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check order error existence for {OrderId} in Redis", orderId);
            return false;
        }
    }

    public async Task<Dictionary<long, string>> LoadAllOrderErrors()
    {
        try
        {
            var errorsDict = await _redisCache.HashGetAllAsync<string>(OrdersWithErrorHashKey);
            var result = new Dictionary<long, string>();

            foreach (var kvp in errorsDict)
            {
                if (long.TryParse(kvp.Key, out var orderId))
                {
                    result[orderId] = kvp.Value;
                }
            }

            _logger.LogInformation("Loaded {Count} order errors from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load order errors from Redis");
            return new Dictionary<long, string>();
        }
    }

    public async Task DeleteOrderError(long orderId)
    {
        try
        {
            await _redisCache.HashDeleteAsync(OrdersWithErrorHashKey, orderId.ToString());
            _logger.LogDebug("Deleted order error for {OrderId} from Redis", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete order error for {OrderId} from Redis", orderId);
        }
    }
}
