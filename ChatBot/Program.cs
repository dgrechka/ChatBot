using ChatBot.Chats;
using ChatBot.Interfaces;
using ChatBot.LLMs;
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

            logger.LogInformation("Environment: {0}", env.EnvironmentName);

            IConfiguration config = builder.Configuration.GetRequiredSection("Settings");
            Settings? settings = config.Get<Settings>();
            if (settings == null)
            {
                logger.LogCritical("Settings can't be found");
                Environment.Exit(1);
            }

            if (settings.TelegramBot != null) {
                if(string.IsNullOrWhiteSpace(settings.TelegramBot.AccessToken))
                {
                    logger.LogCritical("Telegram bot access token is not set");
                    Environment.Exit(1);
                }

                builder.Services
                    .AddSingleton(settings.TelegramBot)
                    .AddHostedService<TelegramBot>();
                logger.LogInformation("Telegram bot is enabled");
            }

            if (settings.Persona?.InlineConfig != null)
            {
                builder.Services
                    .AddSingleton(settings.Persona.InlineConfig)
                    .AddSingleton<ILLMConfigFactory, LLMConfigFactoryInline>();
                
                logger.LogInformation("Persona configuration is enabled");
            }

            if (settings.LLM?.HuggingFace != null)
            {
                if(string.IsNullOrWhiteSpace(settings.LLM.HuggingFace.ApiKey))
                {
                    logger.LogCritical("HuggingFace API key is not set");
                    Environment.Exit(1);
                }

                if (settings.LLM.HuggingFace.ModelName == "Meta-Llama-3-8B-Instruct") {
                    builder.Services
                        .AddSingleton<ILLM, Llama3_8B>((p) => new Llama3_8B(settings.LLM.HuggingFace.ApiKey));
                    logger.LogInformation("HuggingFace Llama3_8B is enabled");
                } else
                {
                    logger.LogCritical("Unsupported HuggingFace model name: {0}", settings.LLM.HuggingFace.ModelName);
                    Environment.Exit(1);
                }
            }

            if(settings.ChatHistoryStorage?.Postgres != null)
            {
                builder.Services
                    .AddSingleton(settings.ChatHistoryStorage.Postgres)
                    .AddSingleton<IChatHistory, PostgresChatHistory>();
                logger.LogInformation("Postgres chat history is enabled");
            } else
            {
                builder.Services.AddSingleton<IChatHistory, MemoryChatHistory>();
                logger.LogWarning("Chat history storage is not configured, using in-memory storage");

            }


            using IHost host = builder.Build();

            await host.RunAsync();

            logger.LogInformation("Graceful shutdown complete. See you!");
        }
    }
}
