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
        private readonly DateTime? _currentTime;

        public Llama3ConvFormatter(bool includeMessageTimestamps, DateTime? currentTime)
        {
            _includeMessageTimestamps = includeMessageTimestamps;
            _currentTime = currentTime;
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
                llmInput.Append($"<|start_header_id|>assistant{FormatTimestamp(_currentTime)}<|end_header_id|>\n\n");
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
                return timestamp.Value.ToString(" - yyyy-MM-dd HH:mm:ssZ ddd", CultureInfo.InvariantCulture);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
