using ChatBot.Billing;
using ChatBot.Chats;
using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.LLMs.DeepInfra;
using ChatBot.Prompt;
using ChatBot.ScheduledTasks;
using ChatBot.Storage;
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

            if (settings.TelegramBot != null)
            {
                if (string.IsNullOrWhiteSpace(settings.TelegramBot.AccessToken))
                {
                    logger.LogCritical("Telegram bot access token is not set");
                    Environment.Exit(1);
                }

                builder.Services
                    .AddSingleton(settings.TelegramBot)
                    .AddHostedService<TelegramBot>();
                logger.LogInformation("Telegram bot is enabled");
            }
            else {
                logger.LogWarning("Telegram bot is not configured");
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

            if (settings.Prompts?.EnableConvSummaryRAG ?? false) {
                builder.Services.AddScoped<ITemplateSource, PriorConversationSummariesRAGTemplateSourceScoped>();
                logger.LogInformation("Prior conversation summaries RAG template source is enabled");
            }

            if(settings.Models == null)
            {
                logger.LogCritical("Models are not configured");
                Environment.Exit(1);
            }

            builder.Services.AddSingleton(settings.Models);

            if (settings.ModelProviders == null)
            {
                logger.LogCritical("Model providers are not configured");
                Environment.Exit(1);
            }

            builder.Services.AddSingleton(settings.ModelProviders);
            builder.Services.AddSingleton<ITextGenerationLLMFactory, TextGenerationLLMFactory>();
            builder.Services.AddSingleton<ITextEmbeddingLLMFactory, TextEmbeddingLLMFactory>();
            builder.Services.AddTransient<IConversationFormatterFactory, ConversationFormatterFactoryTransient>(
                p => new ConversationFormatterFactoryTransient(p.GetRequiredService<UserMessageContext>(), DateTime.UtcNow));

            if (settings?.UseMessageTimestamps ?? false) {
                logger.LogInformation("Message timestamps are enabled");
            } else {
                // TODO: to support messages without timestamps implement corresponding conversation formatter. make factory to support them
                logger.LogCritical("Message timestamps are disabled. not supported");
                Environment.Exit(1);
            }

            if (settings.Persistence?.Postgres != null)
            {
                builder.Services
                    .AddSingleton(settings.Persistence.Postgres)
                    .AddSingleton<PostgresConnection>()
                    .AddSingleton<IBillingLogger, PostgresBillingLogger>()
                    .AddSingleton<IChatHistoryReader, PostgresChatHistory>()
                    .AddSingleton<IChatHistoryWriter>(p => (PostgresChatHistory)p.GetRequiredService<IChatHistoryReader>())
                    .AddSingleton<ISummaryStorage,PostgresSummaryStorage>()
                    .AddScoped<ITemplateSource, RecentMessagesScopedTemplateSource>();
                logger.LogInformation("Postgres billing logging enabled");
                logger.LogInformation("Postgres chat history is enabled");
                logger.LogInformation("Postgres summary storage enabled");

                if (settings.ConversationProcessing?.EnableConvSummaryEmbeddingsGeneration ?? false) {
                    builder.Services.AddSingleton<IEmbeddingStorageWriter, PostgresSummaryEmbeddings>();
                    builder.Services.AddSingleton<IEmbeddingStorageLookup>(s => (IEmbeddingStorageLookup)s.GetRequiredService<IEmbeddingStorageWriter>());
                    logger.LogInformation("Postgres embeddings storage enabled");
                }
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
                logger.LogInformation($"Conversation processing scheduler is enabled. Considering conversation as complete if no messages in {settings.ConversationProcessing.IdleConversationInterval}");

                if(settings.ConversationProcessing.EnableConvSummaryGeneration)
                {
                    builder.Services.AddScoped<IConversationProcessor, GeneralSummaryExtractorScoped>();
                    logger.LogInformation("General summary conversation processing is enabled");
                }

                if (
                    settings.ConversationProcessing.EnableConvSummaryGeneration &&
                    settings.ConversationProcessing.EnableConvSummaryEmbeddingsGeneration
                    )
                {
                    builder.Services.AddSingleton<ISummaryProcessor, EmbeddingSummaryProcessor>();
                    logger.LogInformation("Embedding summary processor is enabled");
                }

                if (settings.ConversationProcessing.UserProfileProperties != null)
                {
                    builder.Services.AddScoped<IConversationProcessor, PersonalityTraitsExtractorScoped>();
                    builder.Services.AddScoped<ITemplateSource, LearnedUserProfileTemplateSourceScoped>();
                    logger.LogInformation("Personality traits conversation processing is enabled");
                }
                else {
                    logger.LogWarning("Personality traits conversation processing is NOT configured. thus DISABLED");
                    // TODO: add correponding template source
                }
            }
            else {
                builder.Services.AddSingleton<IConversationProcessingScheduler, DisabledConversationProcessingScheduler>();
                logger.LogWarning("Conversation processing scheduler is NOT configured. thus DISABLED");
            }

            // activate processing of accumulated conversations
            builder.Services.AddHostedService<StartupProcessing>();

            builder.Services.AddSingleton<ITemplateSource, FileTemplateSource>(p => new FileTemplateSource(System.IO.Path.Combine("Data", "DefaultPrompts")));
            builder.Services.AddScoped<UserMessageContext>();
            builder.Services.AddScoped<ICurrentConversation, CurrentConversationScoped>();
            builder.Services.AddTransient<IPromptCompiler, Prompt.Compiler>();

            using IHost host = builder.Build();

            await host.RunAsync();

            logger.LogInformation("Graceful shutdown complete. See you!");
        }
    }
}
