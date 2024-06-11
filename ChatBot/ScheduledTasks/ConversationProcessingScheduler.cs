using ChatBot.Interfaces;
using ChatBot.LLMs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public interface IConversationProcessor
    {
        Task Process(Chat chat);
    }

    public class ConversationProcessingScheduler: IConversationProcessingScheduler
    {
        private readonly Dictionary<Chat,DateTime> chatToScheduledProcessingTime = [];
        private readonly ILogger<ConversationProcessingScheduler>? _logger;
        private readonly ConversationProcessingSettings _settings;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly IEnumerable<IConversationProcessor> _processors;
        private Timer? _timer;

        public ConversationProcessingScheduler(
            ILogger<ConversationProcessingScheduler>? logger,
            ConversationProcessingSettings settings,
            IEnumerable<IConversationProcessor> processors)
        {
            _logger = logger;
            _settings = settings;
            _processors = processors;
        }

        public async Task SetLatestMessageTime(Chat chat, DateTime time)
        {
            var scheduledRunTime = time + _settings.IdleConversationInterval;

            await _semaphore.WaitAsync();
            chatToScheduledProcessingTime[chat] = scheduledRunTime;
            _semaphore.Release();

            await ScheduleProcessing();
        }

        private async Task Process()
        {
            await _semaphore.WaitAsync();
            try
            {
                var chatsToProcess = chatToScheduledProcessingTime.Where(kv => kv.Value <= DateTime.UtcNow).Select(kv => kv.Key).ToList();
                foreach (var chat in chatsToProcess)
                {
                    _logger?.LogInformation($"Processing conversation {chat}");
                    chatToScheduledProcessingTime.Remove(chat);

                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await Task.WhenAll(_processors.Select(p => p.Process(chat)));
                        sw.Stop();
                        _logger?.LogInformation($"Conversation {chat} processed in {sw.ElapsedMilliseconds}ms");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing conversation {chat}");
                    }
                }
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
            try {
                // Find the next scheduled processing time
                if (chatToScheduledProcessingTime.Count == 0)
                {
                    if(_timer != null)
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
                        timeToNextScheduledProcessing = TimeSpan.Zero;
                    }

                    if (_timer != null)
                    {
                        _timer.Change(timeToNextScheduledProcessing, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        _timer = new Timer(async _ => await this.Process(), null, timeToNextScheduledProcessing, Timeout.InfiniteTimeSpan);
                    }
                }
            }
            finally {
                _semaphore.Release();
            }
        }
    }

    public class ConversationProcessingSettings
    {
        /// <summary>
        /// How long to wait for new messages before considering the conversation as complete and activating the processing of the conversation.
        /// </summary>
        public TimeSpan IdleConversationInterval { get; set; } = TimeSpan.FromHours(1);
    }
}
