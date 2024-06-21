using ChatBot.LLMs;
using ChatBot.Processing.ChatTurn;
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
        Task NotifyLatestMessageTime(Chat chat, DateTime time);
    }

    public class TelegramBot : IHostedService
    {
        private readonly TelegramBotClient _bot;
        private readonly ILogger<TelegramBot>? _logger;
        private readonly ITextGenerationLLMFactory _llmFactory;
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
            ITextGenerationLLMFactory llmFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _bot = new TelegramBotClient(settings.AccessToken);
            _logger = logger;
            _llmFactory = llmFactory;
            _chatHistoryWriter = chatHistoryWriter;
            _conversationProcessor = conversationProcessor;
        }

        private async Task<int> ProcessUpdate(Telegram.Bot.Types.Update update, CancellationToken cancellationToken)
        {
            if (update.Message == null)
            {
                _logger?.LogWarning("Received update without message");
                return update.Id + 1;
            }
            _logger?.LogInformation($"Received message from {update.Message.Chat.Id}: {update.Message.Text}");

            var chat = new Chat("Telegram", update.Message.Chat.Id.ToString());

            using (var scope = _serviceScopeFactory.CreateAsyncScope())
            {
                var _llm = _llmFactory.CreateLLM(TextGenerationLLMRole.ChatTurn);

                var userMessageContext = scope.ServiceProvider.GetRequiredService<UserMessageContext>();
                userMessageContext.Chat = chat;
                userMessageContext.ActiveModel = _llm.Model;
                var userMessage =
                    new Message(
                        update.Message.Date.ToUniversalTime(),
                        Author.User,
                        update.Message.Text ?? string.Empty);
                userMessageContext.Message = userMessage;

                var promptCompiler = scope.ServiceProvider.GetRequiredService<IPromptCompiler>();

                var prompt = await promptCompiler.CompilePrompt($"{_llm.PromptFormatIdentifier}-chat-turn-root", null, cancellationToken);

                _logger?.LogDebug(prompt.ToString());

                // send typing indicator
                _ = _bot.SendChatActionAsync(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken: cancellationToken);
                


                var llmStart = Stopwatch.StartNew();
                var accountingInfo = new AccountingInfo(chat, "ChatTurn");
                var llmTask = _llm.GenerateResponseAsync(prompt, accountingInfo, null, cancellationToken);

                // telegram typing indicator is only shown for 5 seconds, If we did not get llm response before then, re-sending typing
                do {
                    var completeTask = await Task.WhenAny(llmTask, Task.Delay(5000, cancellationToken));
                    if (completeTask != llmTask)
                    {
                        _ = _bot.SendChatActionAsync(update.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        break;
                    }
                } while (true) ;

                var response = llmTask.Result;
                llmStart.Stop();
                _logger?.LogInformation($"LLM response time: {llmStart.ElapsedMilliseconds}ms");

                _logger?.LogInformation($"Sending response to {update.Message.Chat.Id}: {response}");
                var sent = await _bot.SendTextMessageAsync(update.Message.Chat.Id, response);

                var now = DateTime.UtcNow;

                if (sent != null)
                {
                    var saveTask = async () =>
                    {
                        await _chatHistoryWriter.LogMessages(chat,
                            [
                                userMessage,
                                new Message(now, Author.Bot, response)
                            ], cancellationToken);
                        
                        await _conversationProcessor.NotifyLatestMessageTime(chat, now);
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
