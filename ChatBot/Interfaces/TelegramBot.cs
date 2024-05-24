using ChatBot.LLMs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Requests;

namespace ChatBot.Interfaces
{
    public interface IChatHistory {
        Task<IEnumerable<Message>> GetMessages(Chat chat);
        Task LogMessage(Chat chat, Message message);
    }

    public class TelegramBot : IHostedService
    {
        private readonly TelegramBotClient _bot;
        private readonly ILogger<TelegramBot>? _logger;
        private readonly IChatHistory _chatHistory;
        private readonly ILLMConfigFactory _llmConfigFactory;
        private readonly ILLM _llm;

        private readonly CancellationTokenSource _shutdownCTS = new CancellationTokenSource();
        private readonly TaskCompletionSource _shutdownTask = new TaskCompletionSource();


        public TelegramBot(
            ILogger<TelegramBot>? logger,
            TelegramBotSettings settings,
            IChatHistory chatHistory,
            ILLM llm,
            ILLMConfigFactory lLMConfigFactory)
        {
            _bot = new TelegramBotClient(settings.AccessToken);
            _logger = logger;
            _chatHistory = chatHistory;
            _llm = llm;
            _llmConfigFactory = lLMConfigFactory;
        }

        private async Task<int> ProcessUpdate(Telegram.Bot.Types.Update update)
        {
            var now = DateTime.UtcNow;

            if (update.Message == null)
            {
                _logger?.LogWarning("Received update without message");
                return update.Id + 1;
            }
            _logger?.LogInformation($"Received message from {update.Message.Chat.Id}: {update.Message.Text}");

            var chat = new Chat("Telegram", update.Message.Chat.Id.ToString());

            var prevMessagesTask = _chatHistory.GetMessages(chat);

            var promptConfig = await _llmConfigFactory.CreateLLMConfig(chat);

            var messages = (await prevMessagesTask).ToList();

            var userMessage = new Message
            {
                Timestamp = now,
                Author = Author.User,
                Content = update.Message.Text
            };

            messages.Add(userMessage);

            // send typing indicator
            var typingTask = _bot.SendChatActionAsync(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);
            var llmStart = Stopwatch.StartNew();
            var response = await _llm.GenerateResponseAsync(promptConfig, messages);
            llmStart.Stop();
            _logger?.LogInformation($"LLM response time: {llmStart.ElapsedMilliseconds}ms");

            await typingTask;
            _logger?.LogInformation($"Sending response to {update.Message.Chat.Id}: {response}");
            var sent = await _bot.SendTextMessageAsync(update.Message.Chat.Id, response);

            if (sent != null)
            {
                await _chatHistory.LogMessage(chat, userMessage);
                await _chatHistory.LogMessage(chat, new Message
                {
                    Timestamp = now,
                    Author = Author.Bot,
                    Content = response
                });
                _logger?.LogInformation($"Saved conversation turn");
            }

            return update.Id + 1;
        }

        public async Task Run(CancellationToken cancellationToken) {
            int? offset = null;
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await _bot.GetUpdatesAsync(offset, cancellationToken: cancellationToken);
                    var offsets = await Task.WhenAll(updates.Select(ProcessUpdate));
                    if (offsets.Length != 0)
                    {
                        offset = offsets.Max();
                        _logger?.LogInformation($"Processed updates up to {offset}");
                    }

                } catch(Exception e)
                {
                    _logger?.LogError(e, "Error while getting updates");
                }
            }
            _shutdownTask.SetResult();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting...");
            Task.Run(() => Run(_shutdownCTS.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Stopping...");
            _shutdownCTS.Cancel();
            await _shutdownTask.Task;
            _logger?.LogInformation("Stopped");
        }
    }
}
