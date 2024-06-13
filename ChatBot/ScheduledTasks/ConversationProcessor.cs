using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Prompt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public interface ISummaryStorage {
        Task<DateTime?> GetLatestSummary(Chat chat, string summaryId, CancellationToken cancellationToken);

        IAsyncEnumerable<Chat> GetChatsWithSummaries(string summaryId, CancellationToken cancellationToken);

        Task SaveSummary(Chat chat, string summaryId, DateTime time, string summary, CancellationToken cancellationToken);
    }

    public abstract class ConversationProcessorScoped : IConversationProcessor
    {
        protected readonly ISummaryStorage _summaryStorage;
        private readonly IChatHistoryReader _chatHistoryReader;
        private readonly ConversationProcessingSettings _settings;
        protected readonly ILogger _logger;
        protected readonly string _summaryId;
        protected readonly UserMessageContext _context;

        public ConversationProcessorScoped(
            ILogger<ConversationProcessorScoped> logger,
            ISummaryStorage summaryStorage,
            IChatHistoryReader chatHistoryReader,
            ConversationProcessingSettings settings,
            UserMessageContext context,
            string summaryId)
        {
            _logger = logger;
            _summaryStorage = summaryStorage;
            _settings = settings;
            _chatHistoryReader = chatHistoryReader;
            _summaryId = summaryId;
            _context = context;
        }

        public async Task Process(CancellationToken cancellationToken)
        {
            var latestSummary = await _summaryStorage.GetLatestSummary(_context.Chat, _summaryId, cancellationToken);
            // we fetch all messages that appear after the last summary
            var messages = _chatHistoryReader.GetMessagesSince(_context.Chat, latestSummary ?? DateTime.MinValue, cancellationToken);

            // then we cluster them based on their timestamp
            List<Message> conversation = new();
            DateTime? prevMessageTime = null;

            var now = DateTime.UtcNow;

            await foreach (var message in messages)
            {
                if (now - message.Timestamp < _settings.IdleConversationInterval)
                {
                    // the conversation is still ongoing. will not process it yet
                    continue;
                }

                if (conversation.Count != 0 && message.Timestamp - prevMessageTime > _settings.IdleConversationInterval)
                {
                    // process the conversation
                    await ProcessCore(conversation, cancellationToken);
                    conversation.Clear();
                }

                conversation.Add(message);
                prevMessageTime = message.Timestamp;
            }

            if (conversation.Count != 0)
            {
                await ProcessCore(conversation, cancellationToken);
            }
        }

        private async Task ProcessConversation(List<Message> conversation, CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await ProcessCore(conversation, cancellationToken);
            sw.Stop();
            _logger.LogInformation($"{_summaryId} :Processed conversation in {_context.Chat} of {conversation.Count} messages ({conversation[0].Timestamp} - {conversation[conversation.Count-1].Timestamp}) in {sw.ElapsedMilliseconds}ms");
        }

        protected abstract Task ProcessCore(IEnumerable<Message> conversation, CancellationToken cancellationToken);
    }
}
