using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Prompt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public class GeneralSummaryExtractorScoped : ConversationProcessorScoped
    {
        private readonly IPromptCompiler _promptCompiler;
        private readonly ITextGenerationLLM _llm;

        public GeneralSummaryExtractorScoped(
            ILogger<ConversationProcessorScoped> logger,
            ISummaryStorage summaryStorage,
            IChatHistoryReader chatHistoryReader,
            IPromptCompiler promptCompiler,
            UserMessageContext context,
            ITextGenerationLLM llm,
            ConversationProcessingSettings settings)
            : base(logger, summaryStorage, chatHistoryReader, settings, context, "Summary")
        {
            _promptCompiler = promptCompiler;
            _llm = llm;
        }

        protected override async Task ProcessCore(IEnumerable<Message> conversation, CancellationToken cancellationToken)
        {

            var formattedConversation = Helpers.FormatConversation(
                conversation,
                out DateTime? firstMessageTime,
                out DateTime? lastMessageTime,
                out int counter);

            var runtimeTemplates = new Dictionary<string, string>
            {
                { "conversation-to-process", formattedConversation.ToString() }
            };

            var prompt = await _promptCompiler.CompilePrompt("llama3-conversation-summary", runtimeTemplates,  cancellationToken);
            
            var accountingInfo = new AccountingInfo(_context.Chat, "ConversationSummary");
            var callSettings = new CallSettings()
            {
                StopStrings = ["<|eot_id|>", "\n```" ],
            };

            var summary = await _llm.GenerateResponseAsync(prompt, accountingInfo, callSettings, cancellationToken);

            var summaryHeader = $"The conversation started at {Helpers.FormatTimestamp(firstMessageTime)},\nlasted for {Math.Round((lastMessageTime-firstMessageTime).Value.TotalMinutes)} minutes and consisted of {counter} messages.\n\n";

            var wholeSummary = summaryHeader + summary;

            _logger?.LogInformation($"Generated summary for conversation with {counter} messages. generated length {wholeSummary.Length}");

            _logger?.LogDebug(wholeSummary);
            // save summary to storage
            await _summaryStorage.SaveSummary(_context.Chat, _summaryId, lastMessageTime.Value, wholeSummary, cancellationToken);
        }
    }
}
