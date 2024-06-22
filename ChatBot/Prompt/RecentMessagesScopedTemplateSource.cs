using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Processing.ChatTurn;
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
        private readonly ICurrentConversation _conversation;
        private readonly IConversationFormatterFactory _conversationFormatterFactory;
        private readonly UserMessageContext _userMessageContext;

        public RecentMessagesScopedTemplateSource(
            ILogger<RecentMessagesScopedTemplateSource>? logger,
            ICurrentConversation conversation,
            IConversationFormatterFactory conversationFormatterFactory,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _conversation = conversation;
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

            if(_userMessageContext.Message == null)
            {
                _logger?.LogWarning("Message is not set in the context");
                return string.Empty;
            }

            var prevMessages = await _conversation.GetMessages(cancellationToken);

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
