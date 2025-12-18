using CopyTrading.Extensions;
using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.InfluxInterfaces;
using CopyTrading.Repository.SQLInterfaces.Interfaces;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using Serilog;
using Serilog.Ui.Core.Extensions;
using Serilog.Ui.SqliteDataProvider.Extensions;
using Serilog.Ui.Web.Extensions;

namespace CopyTrading;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // SQLite path for logs - check if running in Docker
        var logsDbFullPath = GetSqliteLogsPath();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.SQLite(
                sqliteDbPath: logsDbFullPath,
                tableName: "logs",
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File("logs/copytrading-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(dispose: true));

        // PostgreSQL Settings
        var postgresSettings = _configuration.GetSection("PostgreSQL").Get<PostgreSQLSettings>() ?? new PostgreSQLSettings();
        services.AddSingleton(postgresSettings);

        // Redis Services
        services.AddRedisServices(_configuration);

        services.AddMemoryCache();

        // ASP.NET Core Services
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Blazor Server
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddSignalR();

        // Core Business Services
        services.AddSingleton<IDockerHealthCheckService, DockerHealthCheckService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<ITradeService, TradeService>();
        services.AddSingleton<ICandleService, CandleService>();
        services.AddSingleton<IFillsOrderService, FillsOrderService>();
        services.AddSingleton<ICurrentWalletPositionService, CurrentWalletPositionService>();
        services.AddSingleton<IBaselinePositionService, BaselinePositionService>();

        // Copy Trading Services
        services.AddSingleton<ICopyOrderService, CopyOrderService2>();
        services.AddSingleton<ICopyOrderResultService, CopyOrderResultService>();
        services.AddSingleton<ICopyOrderStorageService, CopyOrderStorageService>();
        services.AddSingleton<IPositionMappingService, PositionMappingService>();
        services.AddSingleton<ICopyTradeWalletSettingsService, CopyTradeWalletSettingsService>();
        services.AddSingleton<IActiveWindowService, ActiveWindowService>();

        // UI Services
        services.AddSingleton<BlazorUI.Services.Interfaces.IRealtimeUpdateService, BlazorUI.Services.RealtimeUpdateService>();

        // HyperLiquid Providers
        services.AddSingleton<IWalletInfoProvider, WalletInfoProvider>();
        services.AddSingleton<OrdersProvider>();  // Регистрация напрямую для OrdersTradesSubscriber
        services.AddSingleton<IOrdersProvider>(sp => sp.GetRequiredService<OrdersProvider>());  // Регистрация через интерфейс
        services.AddSingleton<IExchangeInfoProvider, ExchangeInfoProvider>();
        services.AddSingleton<CandlesProvider>();

        // HyperLiquid Subscribers
        services.AddSingleton<OrderBookSubscriber>();
        services.AddSingleton<IOrdersTradesSubscriber, OrdersTradesSubscriber>();

        // InfluxDB Repositories
        services.AddSingleton<Repository.InfluxInterfaces.IOrderRepository, Repository.Influx.OrderRepository>();
        services.AddSingleton<Repository.InfluxInterfaces.ITradeRepository, Repository.Influx.TradeRepository>();
        services.AddSingleton<ICandlesRepository, Repository.Influx.CandlesRepository>();

        // Redis Repositories
        services.AddSingleton<Repository.RedisInterfaces.IRedisRepository, Repository.Redis.RedisRepository>();

        // Database Repositories - Use PostgreSQL
        services.AddSingleton<Repository.SQLInterfaces.Interfaces.IOrderRepository, Repository.PostgreSQL.OrderRepository>();
        services.AddSingleton<Repository.SQLInterfaces.Interfaces.ITradeRepository, Repository.PostgreSQL.TradeRepository>();
        services.AddSingleton<IWalletInfoRepository, Repository.PostgreSQL.WalletInfoRepository>();
        services.AddSingleton<IWalletSettingsRepository, Repository.PostgreSQL.WalletSettingsRepository>();
        services.AddSingleton<ILogRepository, Repository.PostgreSQL.LogRepository>();

        // Serilog UI - Use SQLite
        var logsDbFullPath = GetSqliteLogsPath();

        services.AddSerilogUi(options =>
        {
            options.UseSqliteServer(opt =>
            {
                opt.WithConnectionString($"Data Source={logsDbFullPath}")
                   .WithTable("logs");
            });
        });
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env, IServiceProvider serviceProvider)
    {
        // Проверка доступности зависимостей (Redis, PostgreSQL) при старте
        var healthCheckService = serviceProvider.GetRequiredService<IDockerHealthCheckService>();
        var healthCheckTask = healthCheckService.CheckAllDependencies();
        healthCheckTask.Wait(); // Блокируем запуск пока не проверим зависимости

        if (!healthCheckTask.Result)
        {
            Log.Warning("⚠️  Приложение запущено с недоступными зависимостями. Некоторые функции могут не работать.");
        }

        // Exception handling и HSTS должны быть первыми
        app.UseExceptionHandler("/Error");
        app.UseHsts();

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        // Serilog UI
        app.UseSerilogUi(option => option.WithHomeUrl(@"/serilog-ui"));

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.RoutePrefix = "swagger";
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<BlazorUI.Hubs.CopyTradingHub>("/copytradinghub");
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });

        // Инициализация singleton сервисов для подписки на события
        // FillsOrderService - корреляция трейдов с ордерами, генерация OrderFinished событий
        // ВАЖНО: RestoreFromRedis() вызывается вручную через UI (страница Redis Admin)
        serviceProvider.GetService<IFillsOrderService>();

        // CopyOrderService - автоматическое копирование ордеров от отслеживаемых кошельков
        serviceProvider.GetService<ICopyOrderService>();

        // CopyOrderStorageService - хранение и управление копируемыми ордерами
        serviceProvider.GetRequiredService<ICopyOrderStorageService>();

        // RealtimeUpdateService - отправка обновлений в UI через SignalR
        serviceProvider.GetRequiredService<BlazorUI.Services.Interfaces.IRealtimeUpdateService>();
    }

    private static string GetSqliteLogsPath()
    {
        // Check if running in Docker container
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

        if (isDocker)
        {
            // Docker path
            return "/app/data/sqlite/logs.db";
        }
        else
        {
            // Local development path
            var logsDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SQLliteBD", "logs.db");
            return Path.GetFullPath(logsDbPath);
        }
    }
}