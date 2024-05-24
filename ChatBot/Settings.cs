﻿using ChatBot.LLMs;
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

        public PersonaConfig? Persona { get; set; }

        public LLMConfig? LLM { get; set; }
    }

    public class LLMConfig { 
        public HuggingFaceLLMConfig? HuggingFace { get; set; }
    }

    public class HuggingFaceLLMConfig
    {
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
    }

    public class TelegramBotSettings
    {
        public string AccessToken { get; set; }
    }

    public class PersonaConfig
    {
        public InlinePersonaConfig? InlineConfig { get; set; }

        // TODO: add database support
    }

    public class InlinePersonaConfig
    {
        public string BotSpecificPrompt { get; set; }
        public string? UnknownUserPrompt { get; set; }

        public Dictionary<string, string> KnownUsersPrompts { get; set; }
    }
}