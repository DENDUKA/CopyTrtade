using CopyTrading.Services.Interfaces;
using Npgsql;
using StackExchange.Redis;
using System.Diagnostics;

namespace CopyTrading.Services;

/// <summary>
/// Сервис для проверки доступности зависимостей при запуске приложения
/// </summary>
public class DockerHealthCheckService : IDockerHealthCheckService
{
    private readonly ILogger<DockerHealthCheckService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _dockerComposePath;

    public DockerHealthCheckService(
        ILogger<DockerHealthCheckService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Путь к docker-compose.yml - на уровень выше от CopyTrading проекта
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _dockerComposePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
    }

    /// <summary>
    /// Проверяет доступность всех необходимых зависимостей
    /// </summary>
    public async Task<bool> CheckAllDependencies()
    {
        _logger.LogInformation("=== Проверка доступности зависимостей ===");

        var redisOk = await CheckRedis();
        var postgresOk = await CheckPostgreSQL();

        var allOk = redisOk && postgresOk;

        var isDockerContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var autoStartDependencies = !string.Equals(
            Environment.GetEnvironmentVariable("CopyTrading__AutoStartDependencies"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        if (!allOk && autoStartDependencies && !isDockerContainer)
        {
            _logger.LogWarning("❌ Некоторые зависимости недоступны. Попытка запустить Docker контейнеры...");

            // Попытка запустить контейнеры
            var started = await TryStartDockerContainers();

            if (started)
            {
                _logger.LogInformation("⏳ Ожидание запуска контейнеров (15 секунд)...");
                await Task.Delay(15000); // Ждем 15 секунд для полного запуска

                // Повторная проверка
                _logger.LogInformation("🔄 Повторная проверка зависимостей...");
                redisOk = await CheckRedis();
                postgresOk = await CheckPostgreSQL();
                allOk = redisOk && postgresOk;
            }
        }
        else if (!allOk)
        {
            _logger.LogWarning("Автозапуск зависимостей отключен. Ожидается, что инфраструктура уже управляется через Docker Compose/Jenkins.");
        }

        if (allOk)
        {
            _logger.LogInformation("✅ Все зависимости доступны");
        }
        else
        {
            _logger.LogError("❌ Не все зависимости доступны. Приложение может работать некорректно.");
        }

        _logger.LogInformation("==========================================");

        return allOk;
    }

    /// <summary>
    /// Проверяет подключение к Redis
    /// </summary>
    public async Task<bool> CheckRedis()
    {
        try
        {
            var redisHost = _configuration["Redis:Host"] ?? "localhost";
            var redisPort = _configuration["Redis:Port"] ?? "6379";
            var redisPassword = _configuration["Redis:Password"];

            var connectionString = string.IsNullOrEmpty(redisPassword)
                ? $"{redisHost}:{redisPort}"
                : $"{redisHost}:{redisPort},password={redisPassword}";

            _logger.LogInformation("Проверка Redis: {ConnectionString}", $"{redisHost}:{redisPort}");

            var options = ConfigurationOptions.Parse(connectionString);
            options.ConnectTimeout = 3000; // 3 секунды
            options.SyncTimeout = 3000;
            options.AbortOnConnectFail = false;

            using var redis = await ConnectionMultiplexer.ConnectAsync(options);
            var db = redis.GetDatabase();
            await db.PingAsync();

            _logger.LogInformation("✅ Redis: Подключение успешно ({Server})", redis.GetEndPoints().FirstOrDefault());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Redis: Не удалось подключиться");
            return false;
        }
    }

    /// <summary>
    /// Проверяет подключение к PostgreSQL
    /// </summary>
    public async Task<bool> CheckPostgreSQL()
    {
        try
        {
            var host = _configuration["PostgreSQL:Host"] ?? "localhost";
            var port = _configuration["PostgreSQL:Port"] ?? "5432";
            var database = _configuration["PostgreSQL:Database"] ?? "copytrading";
            var username = _configuration["PostgreSQL:Username"] ?? "copytrading_user";
            var password = _configuration["PostgreSQL:Password"] ?? "copytrading_password";

            var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};Timeout=3;";

            _logger.LogInformation("Проверка PostgreSQL: {Host}:{Port}/{Database}", host, port, database);

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT version();", connection);
            var version = await cmd.ExecuteScalarAsync();

            _logger.LogInformation("✅ PostgreSQL: Подключение успешно (Version: {Version})", version?.ToString()?.Split('\n')[0]);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ PostgreSQL: Не удалось подключиться");
            return false;
        }
    }

    /// <summary>
    /// Пытается запустить Docker контейнеры через docker-compose
    /// </summary>
    private async Task<bool> TryStartDockerContainers()
    {
        try
        {
            _logger.LogInformation("🐳 Запуск Docker контейнеров: docker-compose up -d");
            _logger.LogInformation("📁 Рабочая директория: {Path}", _dockerComposePath);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = "up -d",
                WorkingDirectory = _dockerComposePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("[docker-compose] {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogWarning("[docker-compose] {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("✅ Docker контейнеры запущены успешно");
                return true;
            }
            else
            {
                _logger.LogError("❌ Не удалось запустить Docker контейнеры. Exit code: {ExitCode}", process.ExitCode);
                if (errorBuilder.Length > 0)
                {
                    _logger.LogError("Ошибки: {Errors}", errorBuilder.ToString());
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при попытке запустить Docker контейнеры");
            _logger.LogWarning("💡 Убедитесь, что Docker Desktop запущен и docker-compose установлен");
            return false;
        }
    }
}
