using ChatBot.LLMs;
using ChatBot.ScheduledTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot
{
    public class Settings
    {
        public TelegramBotSettings? TelegramBot { get; set; }

        public ModelsConfig? Models { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Polymorphic deserialization does not work, this specifying exact type for now.</remarks>
        public Dictionary<string, DeepInfraModelProviderConfig>? ModelProviders { get; set; }

        public PersistenceConfig? Persistence { get; set; }

        public PromptsSettings? Prompts { get; set; }

        public bool? UseMessageTimestamps { get; set; }

        public ConversationProcessingSettings? ConversationProcessing { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(DeepInfraModelProviderConfig), "DeepInfra")]
    public class ModelProviderConfig {
        public string Type { get; set; } = string.Empty;
    }

    public class DeepInfraModelProviderConfig : ModelProviderConfig
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public class PromptsSettings
    {
        public Dictionary<string, string>? Inline { get; set; }
    }

    public class PersistenceConfig
    {
        public PostgresConfig? Postgres { get; set; }
    }

    public class ModelsConfig
    {
        public TextCompletionLLMConfig? ChatTurn { get; set; }
        public TextCompletionLLMConfig? ConvSummary { get; set; }
        public TextCompletionLLMConfig? UserProfileUpdater { get; set; }

        public TextEmbeddingLLMConfig? ConvSummaryEmbedding { get; set; }
    }
    public abstract class LLMConfig<ModelEnum>
    {
        public ModelEnum Model { get; set; } = default!;
        public string Provider { get; set; } = string.Empty;
    }

    public enum TextCompletionModels
    {
        Llama3_8B_instruct,
        Llama3_70B_instruct,
        Qwen2_72B_instruct,
    }

    public enum TextEmbeddingModels
    {
        BGE_M3
    }

    public class TextCompletionLLMConfig: LLMConfig<TextCompletionModels>
    {
        public int? MaxTokensToGenerate { get; set; }
    }

    public class TextEmbeddingLLMConfig : LLMConfig<TextEmbeddingModels>
    {
    }

    public class HuggingFaceLLMConfig
    {
        public string Model { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class DeepInfraLLMConfig
    {
        public string ModelName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int MaxTokensToGenerate { get; set; } = 0;
    }

    public class TelegramBotSettings
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    public class PostgresConfig
    {
        public string Host { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int Port { get; set; }

        public bool UseVectorExtension { get; set; }
    }
}
