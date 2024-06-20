using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Prompt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public class Summary {
        public string RecordId { get; set; }
        public DateTime Time { get; set; }
        public string Content { get; set; }
        public Chat Chat { get; set; }

        public Summary(string recordId, DateTime time, string content, Chat chat) {
            RecordId = recordId;
            Time = time;
            Content = content;
            Chat = chat;
        }
    }

    public interface ISummaryStorage {
        Task<Summary?> GetLatestSummary(Chat chat, string summaryId, CancellationToken cancellationToken);

        Task<Summary?> GetSummaryByRecordId(string recordId, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a recordIds which appeared after the given summaryRecordId
        /// </summary>
        /// <param name="summaryRecordId">null means return all of the summaries</param>
        /// <returns></returns>
        IAsyncEnumerable<string> GetSummaryIdsSince(string summaryId, string? summaryRecordId, CancellationToken cancellationToken);

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
            if(_context.Chat == null)
            {
                _logger?.LogWarning("Chat is not set in the context");
                return;
            }

            var latestSummary = await _summaryStorage.GetLatestSummary(_context.Chat, _summaryId, cancellationToken);
            // we fetch all messages that appear after the last summary
            var messages = _chatHistoryReader.GetMessagesSince(_context.Chat, latestSummary?.Time ?? DateTime.MinValue, cancellationToken);

            await foreach (var conversation in Helpers.ClusterConversations(messages, _settings.IdleConversationInterval))
            {
                await ProcessConversation(conversation, cancellationToken);
            }
        }

        private async Task ProcessConversation(List<Message> conversation, CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await ProcessCore(conversation, cancellationToken);
            sw.Stop();
            _logger.LogInformation($"{_summaryId}: Processed conversation in {_context.Chat} of {conversation.Count} messages ({conversation[0].Timestamp} - {conversation[^1].Timestamp}) in {sw.ElapsedMilliseconds}ms");
        }

        protected abstract Task ProcessCore(IEnumerable<Message> conversation, CancellationToken cancellationToken);
    }
}
