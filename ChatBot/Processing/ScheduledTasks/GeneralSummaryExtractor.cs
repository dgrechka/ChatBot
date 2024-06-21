using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Processing.ChatTurn;
using ChatBot.Prompt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Processing.ScheduledTasks
{
    public class GeneralSummaryExtractorScoped : ConversationProcessorScoped
    {
        private readonly IPromptCompiler _promptCompiler;
        private readonly ITextGenerationLLMFactory _llmFactory;
        private readonly ISummaryProcessor? _summaryProcessor;

        public GeneralSummaryExtractorScoped(
            ILogger<ConversationProcessorScoped> logger,
            ISummaryStorage summaryStorage,
            IChatHistoryReader chatHistoryReader,
            IPromptCompiler promptCompiler,
            UserMessageContext context,
            ITextGenerationLLMFactory llmFactory,
            ISummaryProcessor? summaryProcessor,
            ConversationProcessingSettings settings)
            : base(logger, summaryStorage, chatHistoryReader, settings, context, "Summary")
        {
            _promptCompiler = promptCompiler;
            _llmFactory = llmFactory;
            _summaryProcessor = summaryProcessor;
        }

        protected override async Task ProcessCore(IEnumerable<Message> conversation, CancellationToken cancellationToken)
        {

            var formattedConversation = Helpers.FormatConversation(
                conversation,
                out DateTime? firstMessageTime,
                out DateTime? lastMessageTime,
                out int counter);

            if (firstMessageTime == null || lastMessageTime == null)
            {
                _logger?.LogWarning("No messages in conversation to process");
                return;
            }

            var runtimeTemplates = new Dictionary<string, string>
            {
                { "conversation-to-process", formattedConversation.ToString() }
            };

            var _llm = _llmFactory.CreateLLM(TextGenerationLLMRole.ConvSummary);

            var prompt = await _promptCompiler.CompilePrompt($"{_llm.PromptFormatIdentifier}-conversation-summary", runtimeTemplates, cancellationToken);

            if (_context.Chat == null)
            {
                _logger?.LogWarning("Chat is not set in the context");
                return;
            }

            var accountingInfo = new AccountingInfo(_context.Chat, "ConversationSummary");
            var callSettings = new LLMCallSettings()
            {
                StopStrings = [.. _llm.DefaultStopStrings, "\n```"],
                ProduceJSON = true,
            };

            var summary = await _llm.GenerateResponseAsync(prompt, accountingInfo, callSettings, cancellationToken);

            var summaryHeader = $"The conversation started at {Helpers.FormatTimestamp(firstMessageTime)},\nlasted for {Math.Round((lastMessageTime - firstMessageTime).Value.TotalMinutes)} minutes and consisted of {counter} messages.\n\n";

            var wholeSummary = summaryHeader + summary;

            _logger?.LogInformation($"Generated summary for conversation with {counter} messages. generated length {wholeSummary.Length}");

            _logger?.LogDebug(wholeSummary);
            // save summary to storage
            await _summaryStorage.SaveSummary(_context.Chat, _summaryId, lastMessageTime.Value, wholeSummary, cancellationToken);

            if (_summaryProcessor != null)
            {
                await _summaryProcessor.NotifyNewSummaryPersisted(_summaryId);
            }
        }
    }

    public interface ISummaryProcessor
    {
        Task NotifyNewSummaryPersisted(string summaryId);
    }
}
