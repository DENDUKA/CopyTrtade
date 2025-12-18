namespace CopyTrading.Services.Interfaces;

/// <summary>
/// Сервис для проверки доступности зависимостей (Redis, PostgreSQL)
/// </summary>
public interface IDockerHealthCheckService
{
    /// <summary>
    /// Проверяет доступность всех необходимых зависимостей
    /// </summary>
    /// <returns>True если все зависимости доступны</returns>
    Task<bool> CheckAllDependencies();

    /// <summary>
    /// Проверяет подключение к Redis
    /// </summary>
    Task<bool> CheckRedis();

    /// <summary>
    /// Проверяет подключение к PostgreSQL
    /// </summary>
    Task<bool> CheckPostgreSQL();
}
