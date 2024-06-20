using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class PriorConversationSummariesRAGTemplateSourceScoped : ITemplateSource
    {
        private const string TemplateKey = "prior-conversations";

        private readonly UserMessageContext _userMessageContext;
        private readonly ITextEmbeddingLLMFactory _textEmbeddingLLMFactory;
        private readonly IEmbeddingStorageLookup _embeddingStorageLookup;
        private readonly ICurrentConversation _conversation;
        private readonly ILogger<PriorConversationSummariesRAGTemplateSourceScoped>? _logger;

        public PriorConversationSummariesRAGTemplateSourceScoped(
            ILogger<PriorConversationSummariesRAGTemplateSourceScoped>? logger,
            ISummaryStorage summaryStorage,
            ITextEmbeddingLLMFactory textEmbeddingLLMFactory,
            IEmbeddingStorageLookup embeddingStorageLookup,
            ICurrentConversation conversation,
            UserMessageContext userMessageContext)
        {
            _logger = logger;
            _userMessageContext = userMessageContext;
            _textEmbeddingLLMFactory = textEmbeddingLLMFactory;
            _conversation = conversation;
            _embeddingStorageLookup = embeddingStorageLookup;
        }
        public async Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key != TemplateKey)
            {
                throw new ArgumentException($"Unexpected key: {key}");
            }

            if (_userMessageContext.Chat == null)
            {
                _logger?.LogWarning("Chat is not set in the context");
                return string.Empty;
            }

            if (_userMessageContext.Message == null)
            {
                _logger?.LogWarning("Message is not set in the context");
                return string.Empty;
            }

            var prevMessages = await _conversation.GetMessages(cancellationToken);
            var prevPlusCurrent = prevMessages.Append(_userMessageContext.Message);

            StringBuilder sb = new();
            foreach (var message in prevPlusCurrent)
            {
                sb.AppendLine($"{message.Author} at {Helpers.FormatTimestamp(message.Timestamp)}: {message.Content}");
            }

            var currentConvEmbedding = await _textEmbeddingLLMFactory
                .CreateLLM(TextEmbeddingLLMRole.CurrentConversation)
                .GenerateEmbeddingAsync(
                    sb.ToString(),
                    new AccountingInfo(_userMessageContext.Chat, "CurrentConversationEmbedding"),
                    cancellationToken);

            if (currentConvEmbedding == null)
            {
                _logger?.LogWarning("Failed to generate embedding for current conversation");
                return string.Empty;
            }


            sb.Clear();

            bool first = true;
            await foreach (var summary in _embeddingStorageLookup.GetRelevantSummaries(
                "Summary",
                _userMessageContext.Chat,
                currentConvEmbedding.Select(v => (float)v).ToArray(),
                cancellationToken))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.AppendLine("---");
                }
                sb.AppendLine(summary.Content);
            }

            return sb.ToString();
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == TemplateKey);
        }
    }

    public interface IEmbeddingStorageLookup
    {
        IAsyncEnumerable<Summary> GetRelevantSummaries(string summaryId, Chat chat, float[] queryEmbedding, CancellationToken cancellationToken);
    }
}
