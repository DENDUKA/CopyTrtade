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
    private const string PositionMappingsHashKey = "position_mappings:all";
    private const string BaselinePositionsHashKey = "baseline_positions:all";
    private const string CopyOrdersHashKey = "copy_orders:all";
    private const string CopyOrderResultsHashKey = "copy_order_results:all";
    private const string WalletSettingsHashKey = "wallet_settings:all";
    private const string WalletSnapshotsHashKey = "wallet_snapshots:all";

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

    // ========== POSITION MAPPINGS ==========

    public async Task SavePositionMapping(Models.Models.PositionMapping mapping)
    {
        try
        {
            var key = mapping.GetKey();
            await _redisCache.HashSet(PositionMappingsHashKey, key, mapping);
            _logger.LogDebug("Saved position mapping {Key} to Redis", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save position mapping to Redis");
        }
    }

    public async Task<Models.Models.PositionMapping?> GetPositionMapping(string key)
    {
        try
        {
            var mapping = await _redisCache.HashGetAsync<Models.Models.PositionMapping>(PositionMappingsHashKey, key);
            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get position mapping {Key} from Redis", key);
            return null;
        }
    }

    public async Task<bool> PositionMappingExists(string key)
    {
        try
        {
            return await _redisCache.HashExistsAsync(PositionMappingsHashKey, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check position mapping existence {Key} in Redis", key);
            return false;
        }
    }

    public async Task DeletePositionMapping(string key)
    {
        try
        {
            await _redisCache.HashDeleteAsync(PositionMappingsHashKey, key);
            _logger.LogDebug("Deleted position mapping {Key} from Redis", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete position mapping {Key} from Redis", key);
        }
    }

    public async Task<Dictionary<string, Models.Models.PositionMapping>> LoadAllPositionMappings()
    {
        try
        {
            var mappingsDict = await _redisCache.HashGetAllAsync<Models.Models.PositionMapping>(PositionMappingsHashKey);
            _logger.LogInformation("Loaded {Count} position mappings from Redis", mappingsDict.Count);
            return mappingsDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load position mappings from Redis");
            return new Dictionary<string, Models.Models.PositionMapping>();
        }
    }

    public async Task<Models.Models.PositionMapping[]> GetMappingsByTrader(Models.Values.Wallet traderWallet)
    {
        try
        {
            var allMappings = await LoadAllPositionMappings();
            var traderMappings = allMappings.Values
                .Where(m => m.TraderWallet.Value == traderWallet.Value)
                .ToArray();

            _logger.LogDebug("Found {Count} mappings for trader {Wallet}",
                traderMappings.Length, traderWallet.Value);
            return traderMappings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get mappings for trader {Wallet}", traderWallet.Value);
            return Array.Empty<Models.Models.PositionMapping>();
        }
    }

    // ========== BASELINE POSITIONS ==========

    public async Task SaveBaselinePosition(Models.Values.Wallet wallet, string symbol, decimal quantity)
    {
        try
        {
            var key = $"{wallet.Value}_{symbol}";
            // Convert decimal to string for Redis storage
            await _redisCache.HashSet(BaselinePositionsHashKey, key, quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _logger.LogDebug("Saved baseline position {Key}={Quantity} to Redis", key, quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save baseline position {Wallet}/{Symbol} to Redis", wallet.Value, symbol);
        }
    }

    public async Task<decimal?> GetBaselinePosition(Models.Values.Wallet wallet, string symbol)
    {
        try
        {
            var key = $"{wallet.Value}_{symbol}";
            // Get as string from Redis and parse to decimal
            var quantityStr = await _redisCache.HashGetAsync<string>(BaselinePositionsHashKey, key);
            if (string.IsNullOrEmpty(quantityStr))
                return null;

            if (decimal.TryParse(quantityStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var quantity))
                return quantity;

            _logger.LogWarning("Failed to parse baseline position value '{Value}' for {Wallet}/{Symbol}", quantityStr, wallet.Value, symbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get baseline position {Wallet}/{Symbol} from Redis", wallet.Value, symbol);
            return null;
        }
    }

    public async Task DeleteBaselinePosition(Models.Values.Wallet wallet, string symbol)
    {
        try
        {
            var key = $"{wallet.Value}_{symbol}";
            await _redisCache.HashDeleteAsync(BaselinePositionsHashKey, key);
            _logger.LogDebug("Deleted baseline position {Key} from Redis", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete baseline position {Wallet}/{Symbol} from Redis", wallet.Value, symbol);
        }
    }

    public async Task<Dictionary<string, decimal>> LoadAllBaselinePositions()
    {
        try
        {
            // Get all as strings from Redis
            var positionsStrDict = await _redisCache.HashGetAllAsync<string>(BaselinePositionsHashKey);

            // Parse strings to decimals
            var positionsDict = new Dictionary<string, decimal>();
            foreach (var kvp in positionsStrDict)
            {
                if (decimal.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var quantity))
                {
                    positionsDict[kvp.Key] = quantity;
                }
                else
                {
                    _logger.LogWarning("Failed to parse baseline position value '{Value}' for key '{Key}'", kvp.Value, kvp.Key);
                }
            }

            _logger.LogInformation("Loaded {Count} baseline positions from Redis", positionsDict.Count);
            return positionsDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load baseline positions from Redis");
            return new Dictionary<string, decimal>();
        }
    }

    // ========== COPY ORDERS ==========

    public async Task SaveCopyOrder(Models.Models.Orders.CopyOrderV2 copyOrder)
    {
        try
        {
            await _redisCache.HashSet(CopyOrdersHashKey, copyOrder.OrderId.ToString(), copyOrder);
            _logger.LogDebug("Saved copy order {OrderId} to Redis", copyOrder.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save copy order {OrderId} to Redis", copyOrder.OrderId);
        }
    }

    public async Task<Models.Models.Orders.CopyOrderV2?> GetCopyOrder(long orderId)
    {
        try
        {
            var copyOrder = await _redisCache.HashGetAsync<Models.Models.Orders.CopyOrderV2>(CopyOrdersHashKey, orderId.ToString());
            return copyOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy order {OrderId} from Redis", orderId);
            return null;
        }
    }

    public async Task DeleteCopyOrder(long orderId)
    {
        try
        {
            await _redisCache.HashDeleteAsync(CopyOrdersHashKey, orderId.ToString());
            _logger.LogDebug("Deleted copy order {OrderId} from Redis", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete copy order {OrderId} from Redis", orderId);
        }
    }

    public async Task<Dictionary<long, Models.Models.Orders.CopyOrderV2>> LoadAllCopyOrders()
    {
        try
        {
            var ordersDict = await _redisCache.HashGetAllAsync<Models.Models.Orders.CopyOrderV2>(CopyOrdersHashKey);
            var result = new Dictionary<long, Models.Models.Orders.CopyOrderV2>();

            foreach (var kvp in ordersDict)
            {
                if (long.TryParse(kvp.Key, out var orderId))
                {
                    result[orderId] = kvp.Value;
                }
            }

            _logger.LogInformation("Loaded {Count} copy orders from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load copy orders from Redis");
            return new Dictionary<long, Models.Models.Orders.CopyOrderV2>();
        }
    }

    public async Task<Models.Models.Orders.CopyOrderV2[]> GetCopyOrdersByOriginalId(long originalOrderId)
    {
        try
        {
            var allOrders = await LoadAllCopyOrders();
            var matchingOrders = allOrders.Values
                .Where(o => o.OriginalOrderId == originalOrderId)
                .ToArray();

            _logger.LogDebug("Found {Count} copy orders for original order {OriginalOrderId}",
                matchingOrders.Length, originalOrderId);
            return matchingOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders by original ID {OriginalOrderId}", originalOrderId);
            return Array.Empty<Models.Models.Orders.CopyOrderV2>();
        }
    }

    public async Task<Models.Models.Orders.CopyOrderV2[]> GetCopyOrdersByWalletAndSymbol(Models.Values.Wallet traderWallet, string symbol)
    {
        try
        {
            var allOrders = await LoadAllCopyOrders();
            var matchingOrders = allOrders.Values
                .Where(o => o.OriginalOrder.Wallet.Value == traderWallet.Value
                         && o.OriginalOrder.Symbol == symbol)
                .ToArray();

            _logger.LogDebug("Found {Count} copy orders for {Wallet}/{Symbol}",
                matchingOrders.Length, traderWallet.Value, symbol);
            return matchingOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy orders for {Wallet}/{Symbol}", traderWallet.Value, symbol);
            return Array.Empty<Models.Models.Orders.CopyOrderV2>();
        }
    }

    public async Task DeleteCopyOrders(IEnumerable<long> orderIds)
    {
        try
        {
            foreach (var orderId in orderIds)
            {
                await _redisCache.HashDeleteAsync(CopyOrdersHashKey, orderId.ToString());
            }
            _logger.LogDebug("Deleted {Count} copy orders from Redis", orderIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete multiple copy orders from Redis");
        }
    }

    // ========== COPY ORDER RESULTS ==========

    public async Task SaveCopyOrderResult(Models.Models.CopyOrderResult result)
    {
        try
        {
            await _redisCache.HashSet(CopyOrderResultsHashKey, result.OriginalOrderId, result);
            _logger.LogDebug("Saved copy order result for {OriginalOrderId} to Redis", result.OriginalOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save copy order result for {OriginalOrderId} to Redis", result.OriginalOrderId);
        }
    }

    public async Task<Models.Models.CopyOrderResult?> GetCopyOrderResult(string originalOrderId)
    {
        try
        {
            var result = await _redisCache.HashGetAsync<Models.Models.CopyOrderResult>(CopyOrderResultsHashKey, originalOrderId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get copy order result for {OriginalOrderId} from Redis", originalOrderId);
            return null;
        }
    }

    public async Task DeleteCopyOrderResult(string originalOrderId)
    {
        try
        {
            await _redisCache.HashDeleteAsync(CopyOrderResultsHashKey, originalOrderId);
            _logger.LogDebug("Deleted copy order result for {OriginalOrderId} from Redis", originalOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete copy order result for {OriginalOrderId} from Redis", originalOrderId);
        }
    }

    public async Task<Dictionary<string, Models.Models.CopyOrderResult>> LoadAllCopyOrderResults()
    {
        try
        {
            var resultsDict = await _redisCache.HashGetAllAsync<Models.Models.CopyOrderResult>(CopyOrderResultsHashKey);
            _logger.LogInformation("Loaded {Count} copy order results from Redis", resultsDict.Count);
            return resultsDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load copy order results from Redis");
            return new Dictionary<string, Models.Models.CopyOrderResult>();
        }
    }

    public async Task DeleteCopyOrderResults(IEnumerable<string> originalOrderIds)
    {
        try
        {
            foreach (var originalOrderId in originalOrderIds)
            {
                await _redisCache.HashDeleteAsync(CopyOrderResultsHashKey, originalOrderId);
            }
            _logger.LogDebug("Deleted {Count} copy order results from Redis", originalOrderIds.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete multiple copy order results from Redis");
        }
    }

    // ========== COPY TRADE WALLET SETTINGS ==========

    public async Task SaveWalletSettings(Models.Models.CopyTradeWalletSettings settings)
    {
        try
        {
            await _redisCache.HashSet(WalletSettingsHashKey, settings.Wallet.Value, settings);
            _logger.LogDebug("Saved wallet settings for {Wallet} to Redis", settings.Wallet.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save wallet settings for {Wallet} to Redis", settings.Wallet.Value);
        }
    }

    public async Task<Models.Models.CopyTradeWalletSettings?> GetWalletSettings(Models.Values.Wallet wallet)
    {
        try
        {
            var settings = await _redisCache.HashGetAsync<Models.Models.CopyTradeWalletSettings>(WalletSettingsHashKey, wallet.Value);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wallet settings for {Wallet} from Redis", wallet.Value);
            return null;
        }
    }

    public async Task DeleteWalletSettings(Models.Values.Wallet wallet)
    {
        try
        {
            await _redisCache.HashDeleteAsync(WalletSettingsHashKey, wallet.Value);
            _logger.LogDebug("Deleted wallet settings for {Wallet} from Redis", wallet.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete wallet settings for {Wallet} from Redis", wallet.Value);
        }
    }

    public async Task<Dictionary<Models.Values.Wallet, Models.Models.CopyTradeWalletSettings>> LoadAllWalletSettings()
    {
        try
        {
            var settingsDict = await _redisCache.HashGetAllAsync<Models.Models.CopyTradeWalletSettings>(WalletSettingsHashKey);
            var result = settingsDict.ToDictionary(
                kvp => new Models.Values.Wallet(kvp.Key),
                kvp => kvp.Value
            );
            _logger.LogInformation("Loaded {Count} wallet settings from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallet settings from Redis");
            return new Dictionary<Models.Values.Wallet, Models.Models.CopyTradeWalletSettings>();
        }
    }

    // ========== WALLET POSITION SNAPSHOTS ==========

    public async Task SaveWalletSnapshot(Models.Models.WalletPositionsSnapshot snapshot)
    {
        try
        {
            await _redisCache.HashSet(WalletSnapshotsHashKey, snapshot.Wallet.Value, snapshot);
            _logger.LogDebug("Saved wallet snapshot for {Wallet} to Redis", snapshot.Wallet.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save wallet snapshot for {Wallet} to Redis", snapshot.Wallet.Value);
        }
    }

    public async Task<Models.Models.WalletPositionsSnapshot?> GetWalletSnapshot(Models.Values.Wallet wallet)
    {
        try
        {
            var snapshot = await _redisCache.HashGetAsync<Models.Models.WalletPositionsSnapshot>(WalletSnapshotsHashKey, wallet.Value);
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wallet snapshot for {Wallet} from Redis", wallet.Value);
            return null;
        }
    }

    public async Task DeleteWalletSnapshot(Models.Values.Wallet wallet)
    {
        try
        {
            await _redisCache.HashDeleteAsync(WalletSnapshotsHashKey, wallet.Value);
            _logger.LogDebug("Deleted wallet snapshot for {Wallet} from Redis", wallet.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete wallet snapshot for {Wallet} from Redis", wallet.Value);
        }
    }

    public async Task<Dictionary<Models.Values.Wallet, Models.Models.WalletPositionsSnapshot>> LoadAllWalletSnapshots()
    {
        try
        {
            var snapshotsDict = await _redisCache.HashGetAllAsync<Models.Models.WalletPositionsSnapshot>(WalletSnapshotsHashKey);
            var result = snapshotsDict.ToDictionary(
                kvp => new Models.Values.Wallet(kvp.Key),
                kvp => kvp.Value
            );
            _logger.LogInformation("Loaded {Count} wallet snapshots from Redis", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallet snapshots from Redis");
            return new Dictionary<Models.Values.Wallet, Models.Models.WalletPositionsSnapshot>();
        }
    }

    public async Task ClearAllWalletSnapshots()
    {
        try
        {
            // Загружаем все снапшоты и удаляем по одному
            var snapshots = await LoadAllWalletSnapshots();
            foreach (var wallet in snapshots.Keys)
            {
                await _redisCache.HashDeleteAsync(WalletSnapshotsHashKey, wallet.Value);
            }
            _logger.LogInformation("Cleared {Count} wallet snapshots from Redis", snapshots.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all wallet snapshots from Redis");
        }
    }
}
