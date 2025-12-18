namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис для работы с Redis кэшем
/// Предоставляет методы для STRING, HASH и BATCH операций
/// </summary>
public interface IRedisCacheService
{
    // ========== STRING OPERATIONS ==========

    /// <summary>
    /// Получить объект из Redis по ключу
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Сохранить объект в Redis с опциональным TTL
    /// </summary>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// Удалить ключ из Redis
    /// </summary>
    Task<bool> Delete(string key);

    /// <summary>
    /// Проверить существование ключа
    /// </summary>
    Task<bool> Exists(string key);

    // ========== HASH OPERATIONS (для OrderFills по wallet:symbol) ==========

    /// <summary>
    /// Получить значение поля из Redis Hash
    /// </summary>
    Task<T?> HashGetAsync<T>(string hashKey, string field) where T : class;

    /// <summary>
    /// Установить значение поля в Redis Hash
    /// </summary>
    Task<bool> HashSet<T>(string hashKey, string field, T value) where T : class;

    /// <summary>
    /// Удалить поле из Redis Hash
    /// </summary>
    Task<bool> HashDelete(string hashKey, string field);

    /// <summary>
    /// Проверить существование поля в Redis Hash
    /// </summary>
    Task<bool> HashExists(string hashKey, string field);

    /// <summary>
    /// Получить все поля и значения из Redis Hash
    /// </summary>
    Task<Dictionary<string, T>> HashGetAllAsync<T>(string hashKey) where T : class;

    /// <summary>
    /// Получить список всех ключей (полей) в Redis Hash
    /// </summary>
    Task<List<string>> HashKeys(string hashKey);

    // ========== BATCH OPERATIONS ==========

    /// <summary>
    /// Получить несколько объектов за один запрос
    /// </summary>
    Task<Dictionary<string, T?>> GetManyAsync<T>(IEnumerable<string> keys) where T : class;

    /// <summary>
    /// Сохранить несколько объектов за один запрос
    /// </summary>
    Task SetManyAsync<T>(Dictionary<string, T> keyValues, TimeSpan? expiry = null) where T : class;

    // ========== UTILITY ==========

    /// <summary>
    /// Проверить доступность Redis (ping)
    /// </summary>
    Task<bool> Ping();

    /// <summary>
    /// Получить количество ключей в базе данных
    /// </summary>
    Task<long> GetDatabaseSize();

    /// <summary>
    /// Очистить всю базу данных (использовать только для тестов!)
    /// </summary>
    Task FlushDatabase();
}
