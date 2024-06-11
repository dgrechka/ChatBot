using ChatBot.LLMs;
using ChatBot.Prompt;
using Microsoft.Extensions.DependencyInjection;
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
    public interface IConversationProcessingScheduler {
        Task SetLatestMessageTime(Chat chat, DateTime time);
    }

    public class TelegramBot : IHostedService
    {
        private readonly TelegramBotClient _bot;
        private readonly ILogger<TelegramBot>? _logger;
        private readonly ILLM _llm;
        private readonly IPromptCompiler _promptCompiler;
        private readonly IChatHistoryWriter _chatHistoryWriter;
        private readonly IConversationProcessingScheduler _conversationProcessor;

        private readonly CancellationTokenSource _shutdownCTS = new CancellationTokenSource();
        private readonly TaskCompletionSource _shutdownTask = new TaskCompletionSource();
        private readonly IServiceScopeFactory _serviceScopeFactory;


        public TelegramBot(
            ILogger<TelegramBot>? logger,
            IServiceScopeFactory serviceScopeFactory,
            IConversationProcessingScheduler conversationProcessor,
            IChatHistoryWriter chatHistoryWriter,
            TelegramBotSettings settings,
            ILLM llm)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _bot = new TelegramBotClient(settings.AccessToken);
            _logger = logger;
            _llm = llm;
            _chatHistoryWriter = chatHistoryWriter;
            _conversationProcessor = conversationProcessor;
        }

        private async Task<int> ProcessUpdate(Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            if (update.Message == null)
            {
                _logger?.LogWarning("Received update without message");
                return update.Id + 1;
            }
            _logger?.LogInformation($"Received message from {update.Message.Chat.Id}: {update.Message.Text}");

            var chat = new Chat("Telegram", update.Message.Chat.Id.ToString());

            using (var scope = _serviceScopeFactory.CreateAsyncScope())
            {
                var userMessageContext = scope.ServiceProvider.GetRequiredService<Prompt.UserMessageContext>();
                userMessageContext.Chat = chat;
                var userMessage = new Message
                {
                    Author = Author.User,
                    Content = update.Message.Text ?? string.Empty,
                    Timestamp = now
                };
                userMessageContext.Message = userMessage;

                var promptCompiler = scope.ServiceProvider.GetRequiredService<IPromptCompiler>();

                var prompt = await promptCompiler.CompilePrompt("llama3-chat-turn-root", cancellationToken);

                // send typing indicator
                var typingTask = _bot.SendChatActionAsync(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);
                var llmStart = Stopwatch.StartNew();
                var response = await _llm.GenerateResponseAsync(prompt, cancellationToken, chat);
                llmStart.Stop();
                _logger?.LogInformation($"LLM response time: {llmStart.ElapsedMilliseconds}ms");

                await typingTask;
                _logger?.LogInformation($"Sending response to {update.Message.Chat.Id}: {response}");
                var sent = await _bot.SendTextMessageAsync(update.Message.Chat.Id, response);

                now = DateTime.UtcNow;

                if (sent != null)
                {
                    var saveTask = async () =>
                    {
                        await _chatHistoryWriter.LogMessages(chat, new Message[] {
                            userMessage,
                            new Message
                            {
                                Timestamp = now,
                                Author = Author.Bot,
                                Content = response
                            }}, cancellationToken);

                        await _conversationProcessor.SetLatestMessageTime(chat, now);
                    };

                    // intentionally not awaiting for faster return
                    _ = saveTask();
                    _logger?.LogInformation($"Submitted conv turn for persisting");
                }

                return update.Id + 1;
            }
        }

        public async Task Run(CancellationToken cancellationToken) {
            int? offset = null;
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var updates = await _bot.GetUpdatesAsync(offset, cancellationToken: cancellationToken);
                    var offsets = await Task.WhenAll(updates.Select(u => ProcessUpdate(u, cancellationToken)));
                    if (offsets.Length != 0)
                    {
                        offset = offsets.Max();
                        _logger?.LogInformation($"Processed updates up to {offset}");
                    }
                }
                catch (OperationCanceledException) {
                    _logger?.LogInformation("Cancellation requested, stopping...");
                    break;
                }
                catch (Exception e)
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
