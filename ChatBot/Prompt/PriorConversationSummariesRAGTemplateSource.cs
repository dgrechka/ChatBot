﻿using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Processing.ChatTurn;
using ChatBot.Processing.ScheduledTasks;
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
        private readonly ISummaryStorage _summaryStorage;
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
            _summaryStorage = summaryStorage;
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

            List<Task<List<Summary>>> summaryTasks = new();

            var getRelatedEmbeddings = async (String text, string aim) =>
            {
                var curEmbedding = await _textEmbeddingLLMFactory
                    .CreateLLM(TextEmbeddingLLMRole.CurrentUserMessage)
                    .GenerateEmbeddingAsync(
                        text,
                        new AccountingInfo(_userMessageContext.Chat, aim),
                        cancellationToken);

                List<Summary> result = new List<Summary>();

                if (curEmbedding == null)
                {
                    _logger?.LogWarning("Failed to generate embedding for {aim}", aim);
                    return result;
                }

                await foreach (var summary in _embeddingStorageLookup.GetRelevantSummaries(
                    "Summary",
                    _userMessageContext.Chat,
                    curEmbedding.Select(v => (float)v).ToArray(),
                    cancellationToken))
                {
                    result.Add(summary);
                }

                return result;
            };

            summaryTasks.Add(getRelatedEmbeddings(_userMessageContext.Message.Content, "LatestUserMessageEmbeddingGen"));

            var prevMessages = await _conversation.GetMessages(cancellationToken);
            if (prevMessages?.Length > 0)
            {
                var prevPlusCurrent = prevMessages.Append(_userMessageContext.Message);

                StringBuilder sb = new();
                foreach (var message in prevPlusCurrent)
                {
                    sb.AppendLine($"{message.Author} at {Helpers.FormatTimestamp(message.Timestamp)}: {message.Content}");
                }

                summaryTasks.Add(getRelatedEmbeddings(sb.ToString(), "CurrentConversationEmbeddingGen"));
            }

            var latestDialogSummaryTask = async () =>
            {
                var summary = await _summaryStorage.GetLatestSummary(_userMessageContext.Chat, "Summary", cancellationToken);
                return summary != null ? new List<Summary> { summary } : new List<Summary>();
            };

            summaryTasks.Add(latestDialogSummaryTask());

            var recentDaySummariesTask = async () =>
            {
                var result = new List<Summary>();
                await foreach(var summary in _summaryStorage.GetSummariesSince("Summary", DateTime.UtcNow.AddDays(-1), cancellationToken))
                {
                    result.Add(summary);
                }
                return result;
            };
            summaryTasks.Add(recentDaySummariesTask());

            var summaries = (await Task.WhenAll(summaryTasks))
                .SelectMany(s => s)
                .DistinctBy(s => s.RecordId)
                .OrderBy(s => s.Time)
                .Select(s => $"```conversation\n{s.Content}\n```");

            return string.Join("\n\n", summaries);
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
