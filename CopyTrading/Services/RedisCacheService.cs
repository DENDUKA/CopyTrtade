using System.Text.Json;
using CopyTrading.Services.Interfaces;
using StackExchange.Redis;

namespace CopyTrading.Services;

/// <summary>
/// Реализация сервиса для работы с Redis кэшем
/// </summary>
public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : IRedisCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        IncludeFields = true, // Для поддержки fields в records
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        Converters =
        {
            new OrderFillsJsonConverter() // Кастомный конвертер для OrderFills
        }
    };

    // ========== STRING OPERATIONS ==========

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis GetAsync failed for key={Key}", key);
            return null;
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            return await _db.StringSetAsync(key, json, expiry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis SetAsync failed for key={Key}", key);
            return false;
        }
    }

    public async Task<bool> Delete(string key)
    {
        try
        {
            return await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis DeleteAsync failed for key={Key}", key);
            return false;
        }
    }

    public async Task<bool> Exists(string key)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis ExistsAsync failed for key={Key}", key);
            return false;
        }
    }

    // ========== HASH OPERATIONS ==========

    public async Task<T?> HashGetAsync<T>(string hashKey, string field) where T : class
    {
        try
        {
            var value = await _db.HashGetAsync(hashKey, field);
            if (!value.HasValue)
                return null;

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashGetAsync failed for hash={HashKey} field={Field}", hashKey, field);
            return null;
        }
    }

    public async Task<bool> HashSet<T>(string hashKey, string field, T value) where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            return await _db.HashSetAsync(hashKey, field, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashSetAsync failed for hash={HashKey} field={Field}", hashKey, field);
            return false;
        }
    }

    public async Task<bool> HashDelete(string hashKey, string field)
    {
        try
        {
            return await _db.HashDeleteAsync(hashKey, field);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashDeleteAsync failed for hash={HashKey} field={Field}", hashKey, field);
            return false;
        }
    }

    public async Task<bool> HashExists(string hashKey, string field)
    {
        try
        {
            return await _db.HashExistsAsync(hashKey, field);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashExistsAsync failed for hash={HashKey} field={Field}", hashKey, field);
            return false;
        }
    }

    public async Task<Dictionary<string, T>> HashGetAllAsync<T>(string hashKey) where T : class
    {
        try
        {
            var entries = await _db.HashGetAllAsync(hashKey);
            var result = new Dictionary<string, T>();

            foreach (var entry in entries)
            {
                var obj = JsonSerializer.Deserialize<T>(entry.Value!, _jsonOptions);
                if (obj != null)
                    result[entry.Name!] = obj;
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashGetAllAsync failed for hash={HashKey}", hashKey);
            return new Dictionary<string, T>();
        }
    }

    public async Task<List<string>> HashKeys(string hashKey)
    {
        try
        {
            var keys = await _db.HashKeysAsync(hashKey);
            return keys.Select(k => k.ToString()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis HashKeysAsync failed for hash={HashKey}", hashKey);
            return new List<string>();
        }
    }

    // ========== BATCH OPERATIONS ==========

    public async Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class
    {
        try
        {
            var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
            var values = await _db.StringGetAsync(redisKeys);

            var result = new Dictionary<string, T?>();
            for (int i = 0; i < redisKeys.Length; i++)
            {
                var key = redisKeys[i].ToString();
                if (values[i].HasValue)
                {
                    result[key] = JsonSerializer.Deserialize<T>(values[i]!, _jsonOptions);
                }
                else
                {
                    result[key] = null;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis GetManyAsync failed");
            return keys.ToDictionary(k => k, _ => (T?)null);
        }
    }

    public async Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null) where T : class
    {
        try
        {
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();

            foreach (var kvp in keyValues)
            {
                var json = JsonSerializer.Serialize(kvp.Value, _jsonOptions);
                tasks.Add(batch.StringSetAsync(kvp.Key, json, expiry));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis SetManyAsync failed");
        }
    }

    // ========== UTILITY ==========

    public async Task<bool> Ping()
    {
        try
        {
            var latency = await _db.PingAsync();
            logger.LogDebug("Redis ping: {Latency}ms", latency.TotalMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis Ping failed");
            return false;
        }
    }

    public async Task<long> GetDatabaseSize()
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints().First());
            return await server.DatabaseSizeAsync(_db.Database);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis GetDatabaseSize failed");
            return -1;
        }
    }

    public async Task FlushDatabase()
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints().First());
            await server.FlushDatabaseAsync(_db.Database);
            logger.LogWarning("⚠️ Redis database flushed!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis FlushDatabaseAsync failed");
        }
    }
}
