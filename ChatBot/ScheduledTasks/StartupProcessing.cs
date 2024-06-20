using ChatBot.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public class StartupProcessing : IHostedService
    {
        private readonly IChatHistoryReader _chatHistoryReader;
        private readonly IConversationProcessingScheduler _conversationProcessor;
        private readonly ILogger<StartupProcessing> _logger;
        private Task? _startupProcessing = null;

        public StartupProcessing(
            ILogger<StartupProcessing> logger,
            IConversationProcessingScheduler conversationProcessor,
            IChatHistoryReader chatHistoryReader)
        {
            _chatHistoryReader = chatHistoryReader;
            _conversationProcessor = conversationProcessor;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _startupProcessing = Task.Run(async () =>
            {
                _logger.LogInformation("Scheduling startup processing of all chats");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int counter = 0;
                await foreach (var (chat,lastMessageTime) in _chatHistoryReader.GetChatsLastMessageTime(cancellationToken))
                {
                    await _conversationProcessor.NotifyLatestMessageTime(chat, lastMessageTime);
                    counter++;
                }
                sw.Stop();
                _logger.LogInformation($"Scheduled startup processing of {counter} chats in {sw.ElapsedMilliseconds}ms");
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _startupProcessing  ?? Task.CompletedTask;
        }
    }
}
