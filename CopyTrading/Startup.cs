using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx.Interfaces;
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

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.SQLite(SQLLiteSettings.Path, maxDatabaseSize:0)
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
        services.AddSingleton<ICopyOrderService, CopyOrderService>();
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
        services.AddSingleton<OrdersTradesSubscriber>();

        // InfluxDB Repositories
        services.AddSingleton<IOrderRepository, Repository.Influx.OrderRepository>();
        services.AddSingleton<ITradeRepository, Repository.Influx.TradeRepository>();
        services.AddSingleton<ICandlesRepository, Repository.Influx.CandlesRepository>();

        // SQLite Repositories
        services.AddSingleton<Repository.SQLite.IOrderRepository, Repository.SQLite.OrderRepository>();
        services.AddSingleton<Repository.SQLite.ITradeRepository, Repository.SQLite.TradeRepository>();
        services.AddSingleton<Repository.SQLite.IWalletInfoRepository, Repository.SQLite.WalletInfoRepository>();
        services.AddSingleton<Repository.SQLite.IWalletSettingsRepository, Repository.SQLite.WalletSettingsRepository>();
        services.AddSingleton<Repository.SQLite.ILogRepository, Repository.SQLite.LogRepository>();

        // Serilog UI
        services.AddSerilogUi(options =>
        {
            options.UseSqliteServer(sqlLiteOptions =>
            {
                sqlLiteOptions
                .WithConnectionString($"Data Source={SQLLiteSettings.Path}")
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