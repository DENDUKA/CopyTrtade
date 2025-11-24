using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
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
        services.AddSingleton<OrderService>();
        services.AddSingleton<TradeService>();
        services.AddSingleton<CandleService>();
        services.AddSingleton<FillsOrderService>();
        services.AddSingleton<CurrentWalletPositionService>();

        // Copy Trading Services
        services.AddSingleton<CopyOrderService>();
        services.AddSingleton<CopyOrderResultService>();
        services.AddSingleton<CopyOrderStorageService>();
        services.AddSingleton<PositionMappingService>();
        services.AddSingleton<CopyTradeWalletSettingsService>();

        // UI Services
        services.AddSingleton<BlazorUI.Services.RealtimeUpdateService>();

        // HyperLiquid Providers
        services.AddSingleton<IWalletInfoProvider, WalletInfoProvider>();
        services.AddSingleton<OrdersProvider>();
        services.AddSingleton<IExchangeInfoProvider, ExchangeInfoProvider>();
        services.AddSingleton<CandlesProvider>();

        // HyperLiquid Subscribers
        services.AddSingleton<OrderBookSubscriber>();
        services.AddSingleton<OrdersTradesSubscriber>();

        // InfluxDB Repositories
        services.AddSingleton<OrderRepository>();
        services.AddSingleton<TradeRepository>();
        services.AddSingleton<CandlesRepository>();

        // SQLite Repositories
        services.AddSingleton<Repository.SQLite.OrderRepository>();
        services.AddSingleton<Repository.SQLite.TradeRepository>();
        services.AddSingleton<Repository.SQLite.WalletInfoRepository>();
        services.AddSingleton<Repository.SQLite.WalletSettingsRepository>();
        services.AddSingleton<Repository.SQLite.LogRepository>();

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
        serviceProvider.GetService<FillsOrderService>();

        // CopyOrderService - автоматическое копирование ордеров от отслеживаемых кошельков
        serviceProvider.GetService<CopyOrderService>();

        // CopyOrderStorageService - хранение и управление копируемыми ордерами
        serviceProvider.GetRequiredService<CopyOrderStorageService>();

        // RealtimeUpdateService - отправка обновлений в UI через SignalR
        serviceProvider.GetRequiredService<BlazorUI.Services.RealtimeUpdateService>();
    }
}