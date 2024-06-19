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
        public string Type { get; set; }
    }

    public class DeepInfraModelProviderConfig : ModelProviderConfig
    {
        public string ApiKey { get; set; }
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
    }

    public abstract class LLMConfig<ModelEnum>
    {
        public ModelEnum Model { get; set; }
        public string Provider { get; set; }
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

    public class HuggingFaceLLMConfig
    {
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
    }

    public class DeepInfraLLMConfig
    {
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public int MaxTokensToGenerate { get; set; } = 512;
    }

    public class TelegramBotSettings
    {
        public string AccessToken { get; set; }
    }

    public class PostgresConfig
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
    }
}
