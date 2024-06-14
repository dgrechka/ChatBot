using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class InlineConfigUserSpecificScopedTemplateSource : ITemplateSource
    {
        public static string TemplateKey => "user-basic-introduction";

        private readonly Dictionary<string, string> _inlineConfig;
        private readonly UserMessageContext _userMessageContext;
        private readonly ILogger<InlineConfigUserSpecificScopedTemplateSource>? _logger;
        public InlineConfigUserSpecificScopedTemplateSource(
            ILogger<InlineConfigUserSpecificScopedTemplateSource>? logger,
            Dictionary<string, string> inlineConfig,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _inlineConfig = inlineConfig;
            _userMessageContext = userMessageContext;
        }
        public Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key == TemplateKey)
            {
                var keyToLookup = $"{TemplateKey}-{_userMessageContext.Chat}";
                if (_inlineConfig.ContainsKey(keyToLookup))
                {
                    _logger?.LogInformation($"Found user-specific prompt for chat {_userMessageContext.Chat}");
                    return Task.FromResult(_inlineConfig[keyToLookup]);
                }
                else
                {
                    _logger?.LogInformation($"No user-specific prompt found for chat {_userMessageContext.Chat}. Using stranger prompt");
                    return Task.FromResult("◄unknown-user-prompt►");
                }
            }
            throw new ArgumentException($"Key {key} not found in inline config");
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == TemplateKey);
        }
    }
}
