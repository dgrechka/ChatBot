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
    public class GeneralSummaryProcessorScoped : ConversationProcessorScoped
    {
        private readonly IPromptCompiler _promptCompiler;
        private readonly ILLM _llm;

        public GeneralSummaryProcessorScoped(
            ILogger<ConversationProcessorScoped> logger,
            ISummaryStorage summaryStorage,
            IChatHistoryReader chatHistoryReader,
            IPromptCompiler promptCompiler,
            UserMessageContext context,
            ILLM llm,
            ConversationProcessingSettings settings)
            : base(logger, summaryStorage, chatHistoryReader, settings, context, "Summary")
        {
            _promptCompiler = promptCompiler;
            _llm = llm;
        }

        protected override async Task ProcessCore(IEnumerable<Message> conversation, CancellationToken cancellationToken)
        {
            StringBuilder convSB = new();
            bool first = true;
            DateTime? firstMessageTime = null;
            DateTime? lastMessageTime = null;
            int counter = 0;
            foreach (var message in conversation)
            {
                if (first)
                {
                    convSB.AppendLine($"[start of conversation at {FormatTimestamp(message.Timestamp)}]");
                    firstMessageTime = message.Timestamp;
                    first = false;
                }

                convSB.AppendLine($"{message.Author}:\t{message.Content}\n");
                lastMessageTime = message.Timestamp;
                counter++;
            }

            convSB.AppendLine($"[end of conversation at {FormatTimestamp(lastMessageTime) ?? string.Empty}]");

            var runtimeTemplates = new Dictionary<string, string>
            {
                { "conversation-to-process", convSB.ToString() }
            };

            var prompt = await _promptCompiler.CompilePrompt("llama3-conversation-summary", runtimeTemplates,  cancellationToken);
            
            var accountingInfo = new AccountingInfo(_context.Chat, "ConversationSummary");
            var callSettings = new CallSettings()
            {
                StopStrings = ["<|eot_id|>", "\n```" ],
            };

            var summary = await _llm.GenerateResponseAsync(prompt, accountingInfo, callSettings, cancellationToken);

            var summaryHeader = $"The conversation started at {FormatTimestamp(firstMessageTime)},\nlasted for {Math.Round((lastMessageTime-firstMessageTime).Value.TotalMinutes)} minutes and consisted of {counter} messages.\n\n";

            var wholeSummary = summaryHeader + summary;

            _logger?.LogInformation($"Generated summary for conversation with {counter} messages. generated length {wholeSummary.Length}");

            // save summary to storage
            await _summaryStorage.SaveSummary(_context.Chat, _summaryId, lastMessageTime.Value, wholeSummary, cancellationToken);
        }

        private static string FormatTimestamp(DateTime? timestamp)
        {
            if (timestamp.HasValue)
            {
                return timestamp.Value.ToString("yyyy-MM-dd HH:mm:ssZ (ddd)", CultureInfo.InvariantCulture);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
