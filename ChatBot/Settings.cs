using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot
{
    public class Settings
    {
        public TelegramBotSettings? TelegramBot { get; set; }

        public LLMConfig? LLM { get; set; }

        public PersistenceConfig? Persistence { get; set; }

        public PromptsSettings? Prompts { get; set; }

        public bool? UseMessageTimestamps { get; set; }
    }

    public class PromptsSettings
    {
        public Dictionary<string, string>? Inline { get; set; }
    }

    public class PersistenceConfig
    {
        public PostgresConfig? Postgres { get; set; }
    }

    public class LLMConfig { 
        public HuggingFaceLLMConfig? HuggingFace { get; set; }
        public DeepInfraLLMConfig? DeepInfra { get; set; }
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
