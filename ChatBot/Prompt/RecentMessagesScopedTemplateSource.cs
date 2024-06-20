using ChatBot.Interfaces;
using ChatBot.LLMs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    /// <summary>
    /// To be registered as scoped service, as takes the chat info from the user message context
    /// </summary>
    public class RecentMessagesScopedTemplateSource : ITemplateSource
    {
        public static string TemplateKey => "llm-formatted-recent-messages-with-reply-primer";

        private readonly ILogger<RecentMessagesScopedTemplateSource>? _logger;
        private readonly IChatHistoryReader _chatHistory;
        private readonly IConversationFormatterFactory _conversationFormatterFactory;
        private readonly UserMessageContext _userMessageContext;

        public RecentMessagesScopedTemplateSource(
            ILogger<RecentMessagesScopedTemplateSource>? logger,
            IChatHistoryReader chatHistory,
            IConversationFormatterFactory conversationFormatterFactory,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _chatHistory = chatHistory;
            _userMessageContext = userMessageContext;
            _conversationFormatterFactory = conversationFormatterFactory;
        }

        public async Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key != TemplateKey)
            {
                throw new ArgumentException($"Key {key} is not supported by this source");
            }

            if (_userMessageContext.Chat == null)
            {
                _logger?.LogWarning("Chat is not set in the context");
                return string.Empty;
            }

            List<Message> prevMessages = new();

            await foreach (var prevMessage in _chatHistory.GetMessagesSince(_userMessageContext.Chat, DateTime.UtcNow - TimeSpan.FromHours(1), cancellationToken))
            {
                prevMessages.Add(prevMessage);
            }

            var prevMessagesWithCurrentMessage = prevMessages.Append(_userMessageContext.Message);

            var formatter = _conversationFormatterFactory.GetFormatter();

            return formatter.FormatConversation(prevMessagesWithCurrentMessage!, addResponsePrimer: true);

        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == TemplateKey);
        }
    }

    public interface IConversationFormatter {
        string FormatConversation(IEnumerable<Message> messages, bool addResponsePrimer);
    }
}
