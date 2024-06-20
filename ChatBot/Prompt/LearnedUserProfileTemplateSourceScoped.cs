using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class LearnedUserProfileTemplateSourceScoped : ITemplateSource
    {
        private readonly ISummaryStorage _summaryStorage;
        private readonly UserMessageContext _userMessageContext;
        private readonly ConversationProcessingSettings _conversationProcessingSettings;
        private readonly ILogger<LearnedUserProfileTemplateSourceScoped>? _logger;

        public LearnedUserProfileTemplateSourceScoped(
            ILogger<LearnedUserProfileTemplateSourceScoped>? logger,
            ConversationProcessingSettings conversationProcessingSettings,
            ISummaryStorage summaryStorage,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _summaryStorage = summaryStorage;
            _userMessageContext = userMessageContext;
            _conversationProcessingSettings = conversationProcessingSettings;
        }

        public static string TemplateKey => "learned-user-profile";

        public async Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key == TemplateKey)
            {
                if((_conversationProcessingSettings.UserProfileProperties?.Count ?? 0) == 0)
                {
                    return string.Empty;
                }

                if (_userMessageContext.Chat == null) {
                    _logger?.LogWarning("Chat is not set");
                    return string.Empty;
                }

                var enabledPropertyKeys = new List<string>(_conversationProcessingSettings.UserProfileProperties!.Keys);
                var recentPropertyValues = await Task.WhenAll(
                    enabledPropertyKeys.Select(async propKey => await _summaryStorage.GetLatestSummary(_userMessageContext.Chat, "UserProfile" + propKey, cancellationToken)));

                if(recentPropertyValues.Any(v => !string.IsNullOrWhiteSpace(v?.Content)))
                {
                    var sb = new StringBuilder();
                    for(int i = 0; i < enabledPropertyKeys.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(recentPropertyValues[i]?.Content))
                            continue;
                        sb.AppendLine($"- {enabledPropertyKeys[i]}: {recentPropertyValues[i]!.Content}");
                    }
                    sb.AppendLine();

                    return sb.ToString();
                }

                return string.Empty;
            }
            throw new ArgumentException($"Key {key} not found in learned user profile template source");
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == TemplateKey);
        }
    }
}
