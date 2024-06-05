using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public static class Llama3
    {
        public static string PrepareInput(IPromptConfig config, IEnumerable<Message> messages, bool addResponsePrimer = true)
        {
            StringBuilder llmInput = new StringBuilder("<|begin_of_text|>");

            string systemPrompt = $"{config.BotPersonaSpecificPrompt}\t{config.UserSpecificPrompt}";

            llmInput.Append(FormatMessage("system", systemPrompt, null));
            foreach (Message message in messages)
            {
                DateTime? effectiveTimestamp = config.IncludeMessageTimestamps ? message.Timestamp : null;
                llmInput.Append(FormatMessage(message.Author == Author.User ? "user" : "assistant", message.Content, effectiveTimestamp));
            }

            // reply primer
            if (addResponsePrimer)
            {
                llmInput.Append($"<|start_header_id|>{FormatTimestamp(config.CurrentTime)}assistant<|end_header_id|>\n\n");
            }

            return llmInput.ToString();
        }

        private static string FormatMessage(string author, string text, DateTime? timestamp)
        {
            return $"<|start_header_id|>{FormatTimestamp(timestamp)}{author}<|end_header_id|>\n\n{text}<|eot_id|>"; ;
        }

        private static string FormatTimestamp(DateTime? timestamp)
        {
            if (timestamp.HasValue)
            {
                return timestamp.Value.ToString("yyyy-MM-dd HH:mm:ssZ ddd\n", CultureInfo.InvariantCulture);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
