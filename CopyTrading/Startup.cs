using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.QuartzJobs;
using CopyTrading.Repository.Influx;
using CopyTrading.Services;
using Quartz;

namespace CopyTrading;

public class Startup
{
    private readonly IConfiguration _configuration;

    public Startup(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        //Services
        services.AddSingleton<OrderService>();
        services.AddSingleton<TradeService>();
        services.AddSingleton<CandleService>();
        services.AddSingleton<InformationService>();

        //HyperLiquid Providers
        services.AddSingleton<WalletInfoProvider>();        
        services.AddSingleton<OrdersProvider>(); 
        services.AddSingleton<ExchangeInfoProvider>();
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
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        app.UseExceptionHandler("/Error");

        app.UseHsts();

        app.UseStaticFiles();
    }
}