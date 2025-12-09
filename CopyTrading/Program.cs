using CopyTrading.Infrastructure;
using Serilog;

namespace CopyTrading
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Настраиваем кодировку консоли для корректного отображения русских букв
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            // Настраиваем обработчики необработанных исключений
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "Необработанное исключение привело к завершению приложения");
                Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Fatal(e.Exception, "Необработанное исключение в Task");
                e.SetObserved();
            };

            try
            {
                Log.Information("Запуск приложения CopyTrading");

                // Check if Docker is available and start PostgreSQL container
                var isDockerAvailable = await DockerHelper.IsDockerAvailable();
                if (isDockerAvailable)
                {
                    await DockerHelper.EnsurePostgresRunning();
                }
                else
                {
                    Console.WriteLine("[Docker] Docker is not available. Assuming PostgreSQL is running locally.");
                }

                await CreateHostBuilder(args).Build().RunAsync();
                Log.Information("Приложение CopyTrading завершено корректно");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Приложение завершилось с критической ошибкой");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // config.AddJsonFile("Settings/secrets.json", optional: false, reloadOnChange: false);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }

        //public static void Main(string[] args)
        //{
        //    var builder = WebApplication.CreateBuilder(args);

        //    // Add services to the container.

        //    builder.Services.AddControllers();
        //    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        //    builder.Services.AddEndpointsApiExplorer();
        //    builder.Services.AddSwaggerGen();

        //    var app = builder.Build();

        //    // Configure the HTTP request pipeline.
        //    if (app.Environment.IsDevelopment())
        //    {
        //        app.UseSwagger();
        //        app.UseSwaggerUI();
        //    }

        //    app.UseAuthorization();


        //    app.MapControllers();

        //    app.Run();
        //}
    }
}
