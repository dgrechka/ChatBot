using ChatBot.Prompt;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public class Llama3ConvFormatter : IConversationFormatter
    {
        private readonly bool _includeMessageTimestamps;

        public Llama3ConvFormatter(bool includeMessageTimestamps)
        {
            _includeMessageTimestamps = includeMessageTimestamps;
        }

        public string FormatConversation(IEnumerable<Message> messages, bool addResponsePrimer)
        {
            var llmInput = new StringBuilder();

            foreach (Message message in messages)
            {
                DateTime? effectiveTimestamp = _includeMessageTimestamps ? message.Timestamp : null;
                llmInput.Append(FormatMessage(message.Author == Author.User ? "user" : "assistant", message.Content, effectiveTimestamp));
            }

            // reply primer
            if (addResponsePrimer)
            {
                // TODO: inject time?
                llmInput.Append($"<|start_header_id|>assistant{FormatTimestamp(DateTime.UtcNow)}<|end_header_id|>\n\n");
            }

            return llmInput.ToString();
        }

        private static string FormatMessage(string author, string text, DateTime? timestamp)
        {
            return $"<|start_header_id|>{author}{FormatTimestamp(timestamp)}<|end_header_id|>\n\n{text}<|eot_id|>"; ;
        }

        private static string FormatTimestamp(DateTime? timestamp)
        {
            if (timestamp.HasValue)
            {
                return timestamp.Value.ToString("- yyyy-MM-dd HH:mm:ssZ ddd\n", CultureInfo.InvariantCulture);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
