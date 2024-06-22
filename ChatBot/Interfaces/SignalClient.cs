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

namespace ChatBot.Interfaces
{
    public class IncomingSignalMessage
    {
        public string SourceNumber { get; }
        public ulong Timestamp { get; }
        public string Message { get; }

        public IncomingSignalMessage(string sourceNumber, ulong timestamp, string message)
        {
            SourceNumber = sourceNumber;
            Timestamp = timestamp;
            Message = message;
        }
    }

    public class OutgoingSignalMessage
    {
        public string Recipient { get; }
        public string Message { get; }

        public OutgoingSignalMessage(string recipient, string message)
        {
            Recipient = recipient;
            Message = message;
        }
    }

    public interface ISignalClient
    {
        IAsyncEnumerable<IncomingSignalMessage> GetMessages(CancellationToken cancellationToken);

        Task ConfirmMessageRead(string recepient, ulong timestamp);

        Task ConfirmMessageProcessed(string recepient, ulong timestamp);

        Task SendMessageAsync(OutgoingSignalMessage message, CancellationToken cancellationToken);

        Task SendTypingIndicator(string recepient, CancellationToken cancellationToken);
    }

    public class SignalBot : IHostedService
    {
        private readonly ISignalClient _client;
        private readonly ILogger<SignalBot>? _logger;
        private readonly ITextGenerationLLMFactory _llmFactory;
        private readonly IIdentityMapper _identityMapper;
        private readonly IChatHistoryWriter _chatHistoryWriter;
        private readonly IConversationProcessingScheduler _conversationProcessor;

        private readonly CancellationTokenSource _shutdownCTS = new CancellationTokenSource();
        private readonly TaskCompletionSource _shutdownTask = new TaskCompletionSource();
        private readonly IServiceScopeFactory _serviceScopeFactory;


        public SignalBot(
            ILogger<SignalBot>? logger,
            ISignalClient client,
            IServiceScopeFactory serviceScopeFactory,
            IIdentityMapper identityMapper,
            IConversationProcessingScheduler conversationProcessor,
            IChatHistoryWriter chatHistoryWriter,
            ITextGenerationLLMFactory llmFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _identityMapper = identityMapper;
            _client = client;
            _logger = logger;
            _llmFactory = llmFactory;
            _chatHistoryWriter = chatHistoryWriter;
            _conversationProcessor = conversationProcessor;
        }


        public async Task Run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var messages = _client.GetMessages(cancellationToken);
                    await foreach (var message in messages)
                    {
                        var chat = _identityMapper.TryGetUniformIdentifier(new Chat("Signal", message.SourceNumber.Substring(1)));
                        _logger?.LogInformation($"Received message from {message.SourceNumber} [{chat}]: {message.Message}");

                        _ = _client.ConfirmMessageRead(message.SourceNumber, message.Timestamp);

                        using (var scope = _serviceScopeFactory.CreateAsyncScope())
                        {
                            var llm = _llmFactory.CreateLLM(TextGenerationLLMRole.ChatTurn);

                            var userMessageContext = scope.ServiceProvider.GetRequiredService<UserMessageContext>();
                            userMessageContext.Chat = chat;
                            userMessageContext.ActiveModel = llm.Model;
                            var offset = DateTimeOffset.FromUnixTimeMilliseconds((long)message.Timestamp);

                            var userMessage =
                                new Message(
                                    offset.UtcDateTime,
                                    Author.User,
                                    message.Message ?? string.Empty);
                            userMessageContext.Message = userMessage;

                            var promptCompiler = scope.ServiceProvider.GetRequiredService<IPromptCompiler>();

                            var prompt = await promptCompiler.CompilePrompt($"{llm.PromptFormatIdentifier}-chat-turn-root", null, cancellationToken);

                            _logger?.LogDebug(prompt.ToString());

                            // send typing indicator
                            _ = _client.SendTypingIndicator(message.SourceNumber, cancellationToken);

                            var llmStart = Stopwatch.StartNew();
                            var accountingInfo = new AccountingInfo(chat, "ChatTurn");
                            var llmTask = llm.GenerateResponseAsync(prompt, accountingInfo, null, cancellationToken);

                            // typing indicator is only shown for 10 seconds, If we did not get llm response before then, re-sending typing
                            do
                            {
                                var completeTask = await Task.WhenAny(llmTask, Task.Delay(10000, cancellationToken));
                                if (completeTask != llmTask)
                                {
                                    _ = _client.SendTypingIndicator(message.SourceNumber, cancellationToken);
                                }
                                else
                                {
                                    break;
                                }
                            } while (true);

                            var response = llmTask.Result;

                            llmStart.Stop();
                            _logger?.LogInformation($"LLM response time: {llmStart.ElapsedMilliseconds}ms");

                            _logger?.LogInformation($"Sending response to {message.SourceNumber}: {response}");
                            await _client.SendMessageAsync(new OutgoingSignalMessage(message.SourceNumber, response), cancellationToken);

                            var now = DateTime.UtcNow;

                            await _client.ConfirmMessageProcessed(message.SourceNumber, message.Timestamp);
                            _logger?.LogInformation("Confirmed message processed");

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


                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Cancellation requested, stopping...");
                    break;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Error while getting updates");
                }
            }
            _logger?.LogInformation("Stopped");
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
