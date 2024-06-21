using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Processing.ScheduledTasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Processing.ChatTurn
{
    public interface ICurrentConversation
    {
        Task<Message[]> GetMessages(CancellationToken cancellationToken);
    }

    public class CurrentConversationScoped : ICurrentConversation
    {
        private readonly IChatHistoryReader _chatHistoryReader;
        private readonly ISummaryStorage _summaryStorage;
        private readonly UserMessageContext _userMessageContext;
        private readonly ILogger<ICurrentConversation>? _logger;
        private Message[]? _cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public CurrentConversationScoped(
            IChatHistoryReader chatHistoryReader,
            ISummaryStorage summaryStorage,
            ILogger<ICurrentConversation>? logger,
            UserMessageContext userMessageContext)
        {
            _chatHistoryReader = chatHistoryReader;
            _summaryStorage = summaryStorage;
            _userMessageContext = userMessageContext;
            _logger = logger;
        }

        public async Task<Message[]> GetMessages(CancellationToken cancellationToken)
        {
            if (_userMessageContext.Chat == null)
            {
                throw new InvalidOperationException("Chat is not set in the context");
            }

            await _semaphore.WaitAsync();
            try
            {
                if (_cache == null)
                {

                    var latestSummary = await _summaryStorage.GetLatestSummary(_userMessageContext.Chat, "Summary", cancellationToken);

                    var timeBound = latestSummary?.Time ?? DateTime.MinValue;
                    _logger?.LogInformation("Loading current conversation messages since {timeBound}", timeBound);

                    List<Message> messages = new();
                    await foreach (var message in _chatHistoryReader.GetMessagesSince(_userMessageContext.Chat, timeBound, cancellationToken))
                    {
                        messages.Add(message);
                    }
                    _cache = messages.ToArray();
                    _logger?.LogInformation("Loaded {count} messages", _cache.Length);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return _cache ?? [];
        }
    }
}
