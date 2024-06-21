using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Processing.ChatTurn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Processing.ScheduledTasks
{
    public interface IConversationProcessor
    {
        /// <summary>
        /// Chat is supposed to be extracted from scope via UserMessageContext
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Process(CancellationToken cancellationToken);
    }

    public class ConversationProcessingScheduler : IConversationProcessingScheduler
    {
        private readonly Dictionary<Chat, DateTime> chatToScheduledProcessingTime = [];
        private readonly ILogger<ConversationProcessingScheduler>? _logger;
        private readonly ConversationProcessingSettings _settings;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private Timer? _timer;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ConversationProcessingScheduler(
            ILogger<ConversationProcessingScheduler>? logger,
            ConversationProcessingSettings settings,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _settings = settings;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task NotifyLatestMessageTime(Chat chat, DateTime time)
        {
            var scheduledRunTime = time + _settings.IdleConversationInterval;

            await _semaphore.WaitAsync();
            chatToScheduledProcessingTime[chat] = scheduledRunTime;
            _semaphore.Release();
            _logger?.LogInformation($"Re-scheduling processing of conversation {chat} at {scheduledRunTime}");

            await ScheduleProcessing();
        }

        private async Task Process()
        {
            await _semaphore.WaitAsync();
            try
            {
                var chatsToProcess = chatToScheduledProcessingTime.Where(kv => kv.Value <= DateTime.UtcNow).Select(kv => kv.Key).ToList();
                _logger?.LogInformation($"Waking up. {chatsToProcess.Count} chats to process for new conversations.");
                foreach (var chat in chatsToProcess)
                {
                    chatToScheduledProcessingTime.Remove(chat);

                    var sw = Stopwatch.StartNew();
                    using (var scope = _serviceScopeFactory.CreateAsyncScope())
                    {
                        var userMessageContext = scope.ServiceProvider.GetRequiredService<UserMessageContext>();
                        userMessageContext.Chat = chat;

                        var authorization = scope.ServiceProvider.GetRequiredService<IChatAuthorization>();
                        if(!await authorization.IsAuthorized())
                        {
                            _logger?.LogInformation($"Chat {chat} is not authorized. Skipping processing.");
                            continue;
                        }

                        var _processors = scope.ServiceProvider.GetServices<IConversationProcessor>().ToList();
                        _logger?.LogInformation($"Processing conversation {chat} with {_processors.Count} processors");
                        try
                        {
                            await Task.WhenAll(_processors.Select(p => p.Process(CancellationToken.None)));
                            sw.Stop();
                            _logger?.LogInformation($"Conversation {chat} processed in {sw.ElapsedMilliseconds}ms");
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error processing conversation {chat}");
                        }
                    }
                }
                _logger?.LogInformation($"Processed {chatsToProcess.Count} chats");
            }
            finally
            {
                _semaphore.Release();
            }

            await ScheduleProcessing();
        }

        private async Task ScheduleProcessing()
        {
            await _semaphore.WaitAsync();
            try
            {
                // Find the next scheduled processing time
                if (chatToScheduledProcessingTime.Count == 0)
                {
                    if (_timer != null)
                    {
                        await _timer.DisposeAsync();
                        _timer = null;
                    }
                }
                else
                {
                    var nextScheduledProcessingTime = chatToScheduledProcessingTime.Values.Min();

                    var timeToNextScheduledProcessing = nextScheduledProcessingTime - DateTime.UtcNow;
                    if (timeToNextScheduledProcessing < TimeSpan.Zero)
                    {
                        // We are already late, process now
                        // but let other calls to batch (useful for startup catchup)
                        timeToNextScheduledProcessing = TimeSpan.FromSeconds(5);
                    }

                    if (_timer != null)
                    {
                        _timer.Change(timeToNextScheduledProcessing, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        _timer = new Timer(async _ => await Process(), null, timeToNextScheduledProcessing, Timeout.InfiniteTimeSpan);
                    }
                    _logger?.LogInformation($"Next conversation processing scheduled in {timeToNextScheduledProcessing}");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public class DisabledConversationProcessingScheduler : IConversationProcessingScheduler
    {
        public Task NotifyLatestMessageTime(Chat chat, DateTime time)
        {
            return Task.CompletedTask;
        }
    }

    public class ConversationProcessingSettings
    {
        /// <summary>
        /// How long to wait for new messages before considering the conversation as complete and activating the processing of the conversation.
        /// </summary>
        public TimeSpan IdleConversationInterval { get; set; } = TimeSpan.FromHours(1);

        public bool EnableConvSummaryGeneration { get; set; }

        public bool EnableConvSummaryEmbeddingsGeneration { get; set; }

        public Dictionary<string, string>? UserProfileProperties { get; set; }
    }
}
