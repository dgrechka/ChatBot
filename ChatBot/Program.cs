using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChatBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            IHostEnvironment env = builder.Environment;

            builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, false)
                .AddEnvironmentVariables(prefix: "CHAT_BOT_")
                .AddCommandLine(args);

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();

            IConfiguration config = builder.Configuration.GetRequiredSection("Settings");
            Settings? settings = config.Get<Settings>();
            if (settings == null)
            {
                logger.LogCritical("Settings can't be found");
                Environment.Exit(1);
            }

            using IHost host = builder.Build();

            await host.RunAsync();
        }
    }
}
