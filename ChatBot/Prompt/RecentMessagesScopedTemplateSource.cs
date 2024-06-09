﻿using ChatBot.Interfaces;
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
        private readonly IChatHistory _chatHistory;
        private readonly IConversationFormatter _conversationFormatter;
        private readonly UserMessageContext _userMessageContext;

        public RecentMessagesScopedTemplateSource(
            ILogger<RecentMessagesScopedTemplateSource>? logger,
            IChatHistory chatHistory,
            IConversationFormatter conversationFormatter,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _chatHistory = chatHistory;
            _userMessageContext = userMessageContext;
            _conversationFormatter = conversationFormatter;
        }

        public async Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key != TemplateKey)
            {
                throw new ArgumentException($"Key {key} is not supported by this source");
            }

            var prevMessages = await _chatHistory.GetMessages(_userMessageContext.Chat, cancellationToken);

            var prevMessagesWithCurrentMessage = prevMessages.Append(_userMessageContext.Message);

            return _conversationFormatter.FormatConversation(prevMessagesWithCurrentMessage, addResponsePrimer: true);

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
