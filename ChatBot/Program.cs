using ChatBot.Billing;
using ChatBot.Chats;
using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Persistence;
using ChatBot.Prompt;
using ChatBot.ScheduledTasks;
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

            if (settings.Prompts?.Inline != null)
            {
                builder.Services
                    .AddSingleton<ITemplateSource, DictionaryTemplateSource>(p => new DictionaryTemplateSource(settings.Prompts?.Inline!))
                    .AddScoped<ITemplateSource, InlineConfigUserSpecificScopedTemplateSource>(p => new InlineConfigUserSpecificScopedTemplateSource(
                        p.GetRequiredService<ILogger<InlineConfigUserSpecificScopedTemplateSource>>(),
                        settings.Prompts?.Inline!,
                        p.GetRequiredService<Prompt.UserMessageContext>()));
                
                logger.LogInformation($"{settings.Prompts?.Inline.Count} prompt templates loaded from .NET configuration");
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
                        .AddSingleton<ILLM, LLMs.HuggingFace.Llama3_8B>((p) => new LLMs.HuggingFace.Llama3_8B(settings.LLM.HuggingFace.ApiKey));
                    logger.LogInformation("HuggingFace Llama3_8B is enabled");
                } else
                {
                    logger.LogCritical("Unsupported HuggingFace model name: {0}", settings.LLM.HuggingFace.ModelName);
                    Environment.Exit(1);
                }
            }

            if (settings.LLM?.DeepInfra != null)
            {
                if (string.IsNullOrWhiteSpace(settings.LLM.DeepInfra.ApiKey))
                {
                    logger.LogCritical("DeepInfra API key is not set");
                    Environment.Exit(1);
                }

                var modelFlavor = settings.LLM.DeepInfra?.ModelName switch
                {
                    "Llama-3-8B-Instruct" => LLMs.DeepInfra.Llama3Flavor.Instruct_8B,
                    "Llama-3-70B-Instruct" => LLMs.DeepInfra.Llama3Flavor.Instruct_70B,
                    _ => throw new NotSupportedException($"Unsupported DeepInfra model name: {settings.LLM.DeepInfra?.ModelName ?? string.Empty}")
                };

                builder.Services
                    .AddSingleton<ILLM, LLMs.DeepInfra.Llama3Client>((p) => new LLMs.DeepInfra.Llama3Client(
                        p.GetRequiredService<ILogger<LLMs.DeepInfra.Llama3Client>>(),
                        p.GetRequiredService<IBillingLogger>(),
                        settings.LLM.DeepInfra.ApiKey,
                        settings.LLM.DeepInfra.MaxTokensToGenerate,
                        modelFlavor
                        ))
                    .AddSingleton<IConversationFormatter,Llama3ConvFormatter>(p => new Llama3ConvFormatter(settings?.UseMessageTimestamps ?? false, DateTime.UtcNow));
                logger.LogInformation("DeepInfra Llama3Client is enabled (flavor {0}; max tokens {1})", modelFlavor, settings.LLM.DeepInfra.MaxTokensToGenerate);
            }

            if (settings?.UseMessageTimestamps ?? false) {
                logger.LogInformation("Message timestamps are enabled");
            } else {
                logger.LogInformation("Message timestamps are disabled");
            }

            if (settings.Persistence?.Postgres != null)
            {
                builder.Services
                    .AddSingleton(settings.Persistence.Postgres)
                    .AddSingleton<PostgresConnection>()
                    .AddSingleton<IBillingLogger, PostgresBillingLogger>()
                    .AddSingleton<IChatHistoryReader, PostgresChatHistory>()
                    .AddSingleton<IChatHistoryWriter>(p => (PostgresChatHistory)p.GetRequiredService<IChatHistoryReader>())
                    .AddScoped<ITemplateSource, RecentMessagesScopedTemplateSource>();
                logger.LogInformation("Postgres billing logging enabled");
                logger.LogInformation("Postgres chat history is enabled");
            } else
            {
                builder.Services.AddSingleton<IChatHistoryReader, MemoryChatHistory>();
                builder.Services.AddSingleton<IChatHistoryWriter>(p => (MemoryChatHistory)p.GetRequiredService<IChatHistoryReader>());
                logger.LogWarning("Chat history storage is not configured, using in-memory storage");
            }

            if (settings.ConversationProcessing != null)
            {
                builder.Services.AddSingleton<IConversationProcessingScheduler, ConversationProcessingScheduler>();
                builder.Services.AddSingleton(settings.ConversationProcessing);
                logger.LogInformation("Conversation processing scheduler is enabled");
            }
            else {
                logger.LogWarning("Conversation processing scheduler is NOT configured. thus DISABLED");
            }

            builder.Services.AddSingleton<ITemplateSource, FileTemplateSource>(p => new FileTemplateSource(System.IO.Path.Combine("Data", "DefaultPrompts")));
            builder.Services.AddScoped<Prompt.UserMessageContext>();
            builder.Services.AddTransient<IPromptCompiler, Prompt.Compiler>();

            using IHost host = builder.Build();

            await host.RunAsync();

            logger.LogInformation("Graceful shutdown complete. See you!");
        }
    }
}
