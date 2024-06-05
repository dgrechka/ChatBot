using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public class LLMConfigFactoryInline : ILLMConfigFactory
    {
        private readonly ILogger<LLMConfigFactoryInline>? _logger;
        private readonly InlinePersonaConfig _config;
        private readonly bool useMessageTimestamps;

        private readonly string _defaultUnknownUserPrompt = @"You are prohibited to do any conversation with this user as the user is unknown stranger for you.
            You can't provide any information to such user. You must politely decline continuing the conversation for *every* message of the user. Make your answer short.";

        public LLMConfigFactoryInline(ILogger<LLMConfigFactoryInline>? logger, InlinePersonaConfig config, bool useMessageTimestamps)
        {
            _logger = logger;
            _config = config;
            this.useMessageTimestamps = useMessageTimestamps;
        }
        public Task<PromptConfig> CreateLLMConfig(Chat chat)
        {
            var chatStr = chat.ToString();
            PromptConfig result = new PromptConfig() {
                BotPersonaSpecificPrompt = _config.BotSpecificPrompt
            };

            if (!(_config.KnownUsersPrompts?.TryGetValue(chatStr.ToLower(), out var userPrompt) ?? false))
            {
                if (_config.UnknownUserPrompt != null)
                {
                    _logger?.LogInformation("User {0} not found in known users, using unknown user config", chatStr);
                    result.UserSpecificPrompt = _config.UnknownUserPrompt;
                }
                else
                {
                    _logger?.LogInformation("User {0} not found in known users, using default unknown user config", chatStr);
                    result.UserSpecificPrompt = _defaultUnknownUserPrompt;
                }
            }
            else {
                _logger?.LogInformation("User {0} found in known users, using user config", chatStr);
                result.UserSpecificPrompt = userPrompt;
            }

            result.Chat = chat;
            result.IncludeMessageTimestamps = useMessageTimestamps;

            return Task.FromResult(result);
        }
    }
}
