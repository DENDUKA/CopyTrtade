namespace CopyTrading.Settings;

/// <summary>
/// Настройки подключения к Redis для кэширования состояния в памяти
/// </summary>
public class RedisSettings
{
    /// <summary>
    /// Хост Redis сервера (по умолчанию: localhost)
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Порт Redis сервера (по умолчанию: 6379)
    /// </summary>
    public int Port { get; set; } = 6379;

    /// <summary>
    /// Пароль для подключения к Redis (если требуется)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Номер базы данных Redis (по умолчанию: 0)
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Таймаут подключения в миллисекундах (по умолчанию: 5000)
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Таймаут синхронных операций в миллисекундах (по умолчанию: 5000)
    /// </summary>
    public int SyncTimeout { get; set; } = 5000;

    /// <summary>
    /// Строит connection string для StackExchange.Redis из настроек
    /// </summary>
    public string GetConnectionString()
    {
        var connectionString = $"{Host}:{Port}";

        if (!string.IsNullOrEmpty(Password))
            connectionString += $",password={Password}";

        connectionString += $",defaultDatabase={Database}";
        connectionString += $",connectTimeout={ConnectTimeout}";
        connectionString += $",syncTimeout={SyncTimeout}";
        connectionString += ",abortConnect=false"; // Не падать если Redis недоступен при старте

        return connectionString;
    }
}
