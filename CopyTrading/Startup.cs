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
        services.AddLogging(loggingBuilder =>
            loggingBuilder.AddSerilog(dispose: true));

        services.AddMemoryCache();

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Blazor Server
        services.AddRazorPages();
        services.AddServerSideBlazor();

        // SignalR (для real-time обновлений)
        services.AddSignalR();

        // UI Services
        services.AddSingleton<BlazorUI.Services.RealtimeUpdateService>();

        //Services
        services.AddSingleton<OrderService>();
        services.AddSingleton<TradeService>();
        services.AddSingleton<CopyOrderService>();
        services.AddSingleton<CopyOrderResultService>();
        services.AddSingleton<FillsOrderService>();
        services.AddSingleton<CandleService>();
        services.AddSingleton<CurrentWalletPositionService>();
        services.AddSingleton<PositionMappingService>();
        services.AddSingleton<CopyOrderStorageService>();

        //HyperLiquid Providers
        services.AddSingleton<IWalletInfoProvider, WalletInfoProvider>();        
        services.AddSingleton<OrdersProvider>(); 
        services.AddSingleton<IExchangeInfoProvider, ExchangeInfoProvider>();
        services.AddSingleton<CandlesProvider>();

        //HyperLiquid Subscribers
        services.AddSingleton<OrderBookSubscriber>();
        services.AddSingleton<OrdersTradesSubscriber>();

        //InfluxDB
        services.AddSingleton<OrderRepository>();
        services.AddSingleton<TradeRepository>();
        services.AddSingleton<CandlesRepository>();

        //SQL
        services.AddSingleton<Repository.SQLite.OrderRepository>();
        services.AddSingleton<Repository.SQLite.TradeRepository>();
        services.AddSingleton<Repository.SQLite.WalletInfoRepository>();

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
        app.UseHttpsRedirection();

        // Перемещаем UseStaticFiles() перед Serilog UI
        app.UseStaticFiles();

        app.UseRouting();

        // Serilog UI должен быть зарегистрирован ДО endpoints
        app.UseSerilogUi(option=> option.WithHomeUrl(@"/serilog-ui"));

        // Swagger доступен по /swagger, но не запускается автоматически
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.RoutePrefix = "swagger"; // Swagger доступен по /swagger
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();

            // SignalR Hub для real-time обновлений
            endpoints.MapHub<BlazorUI.Hubs.CopyTradingHub>("/copytradinghub");

            // Blazor Server endpoints
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });

        app.UseExceptionHandler("/Error");

        app.UseHsts();

        serviceProvider.GetService<FillsOrderService>();
        serviceProvider.GetService<CopyOrderService>();

        // Инициализируем CopyOrderStorageService для подписки на события создания/закрытия копируемых ордеров
        serviceProvider.GetRequiredService<CopyOrderStorageService>();

        // Инициализируем RealtimeUpdateService для подписки на DataBusEvents
        serviceProvider.GetRequiredService<BlazorUI.Services.RealtimeUpdateService>();
    }
}