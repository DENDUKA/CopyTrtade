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
using Serilog.Ui.PostgreSqlProvider.Extensions;
using Serilog.Ui.Web.Extensions;

namespace CopyTrading;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var postgresSettings = configuration.GetSection("PostgreSQL").Get<PostgreSQLSettings>() ?? new PostgreSQLSettings();
        var connectionString = postgresSettings.GetConnectionString();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.PostgreSQL(
                connectionString: connectionString,
                tableName: "Logs",
                needAutoCreateTable: true)
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

        // Database Repositories - Use PostgreSQL
        services.AddSingleton<Repository.SQLInterfaces.Interfaces.IOrderRepository, Repository.PostgreSQL.OrderRepository>();
        services.AddSingleton<Repository.SQLInterfaces.Interfaces.ITradeRepository, Repository.PostgreSQL.TradeRepository>();
        services.AddSingleton<IWalletInfoRepository, Repository.PostgreSQL.WalletInfoRepository>();
        services.AddSingleton<IWalletSettingsRepository, Repository.PostgreSQL.WalletSettingsRepository>();
        services.AddSingleton<ILogRepository, Repository.PostgreSQL.LogRepository>();

        // Serilog UI - Use PostgreSQL
        services.AddSerilogUi(options =>
        {
            options.UseNpgSql(opt =>
            {
                opt.WithConnectionString(postgresSettings.GetConnectionString())
                   .WithTable("Logs");
            });
        });
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env, IServiceProvider serviceProvider)
    {
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
        serviceProvider.GetService<IFillsOrderService>();

        // CopyOrderService - автоматическое копирование ордеров от отслеживаемых кошельков
        serviceProvider.GetService<ICopyOrderService>();

        // CopyOrderStorageService - хранение и управление копируемыми ордерами
        serviceProvider.GetRequiredService<ICopyOrderStorageService>();

        // RealtimeUpdateService - отправка обновлений в UI через SignalR
        serviceProvider.GetRequiredService<BlazorUI.Services.Interfaces.IRealtimeUpdateService>();
    }
}