using CopyTrading.Providers.Hyperliquid.Providers;
using CopyTrading.Providers.Hyperliquid.Subscribers;
using CopyTrading.Repository.Influx;
using CopyTrading.Services;

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