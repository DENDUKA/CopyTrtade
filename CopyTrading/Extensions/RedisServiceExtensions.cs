using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using StackExchange.Redis;

namespace CopyTrading.Extensions;

/// <summary>
/// Extension методы для регистрации Redis сервисов в DI контейнере
/// </summary>
public static class RedisServiceExtensions
{
    /// <summary>
    /// Добавляет Redis сервисы в DI контейнер
    /// </summary>
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Redis Settings
        var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>() ?? new RedisSettings();
        services.AddSingleton(redisSettings);

        // Redis Connection (Singleton)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var settings = sp.GetRequiredService<RedisSettings>();
            var logger = sp.GetRequiredService<ILogger<Startup>>();

            try
            {
                var connection = ConnectionMultiplexer.Connect(settings.GetConnectionString());
                var endpoints = string.Join(", ", connection.GetEndPoints().Select(ep => ep.ToString()));
                logger.LogInformation("✅ Redis connected: {Endpoints}", endpoints);
                return connection;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Failed to connect to Redis. Application will continue without cache.");
                throw; // Приложение не должно стартовать без Redis
            }
        });

        // Redis Cache Service
        services.AddSingleton<IRedisCacheService, RedisCacheService>();

        return services;
    }
}
