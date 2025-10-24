using CopyTrading.Providers.Hyperliquid.Interfaces;
using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.QuartzJobs;
using CopyTrading.Repository.Influx;
using CopyTrading.Services;
using CopyTrading.Services.Interfaces;
using CopyTrading.Settings;
using Quartz;
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

        //Services
        services.AddSingleton<OrderService>();
        services.AddSingleton<TradeService>();
        services.AddSingleton<CopyOrderService>();
        services.AddSingleton<FillsOrderService>();
        services.AddSingleton<CandleService>();
        services.AddSingleton<InformationService>();
        services.AddSingleton<CurrentWalletPositionService>();
        services.AddSingleton<PositionMappingService>();

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

        // Quartz
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();

            var jobKey = new JobKey("CollectTradeInfo");
            q.AddJob<CollectTradeInfoJob>(opts => opts.WithIdentity(jobKey));

            // Триггер для запуска каждые 20 минут
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("CollectTradeInfo-trigger")
                .WithCronSchedule("0 0/20 * * * ?"));

            // Отдельный триггер для немедленного запуска при старте
            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("CollectTradeInfo-startup-trigger")
                .StartNow());
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = false);

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
        // Перемещаем UseStaticFiles() перед Serilog UI
        app.UseStaticFiles();
        
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        app.UseSerilogUi(option=> option.WithHomeUrl(@"/serilog-ui"));

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseExceptionHandler("/Error");

        app.UseHsts();

        serviceProvider.GetService<FillsOrderService>();
        serviceProvider.GetService<CopyOrderService>();
    }
}