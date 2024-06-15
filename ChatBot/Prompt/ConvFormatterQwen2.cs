using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class ConvFormatterQwen2 : IConversationFormatter
    {
        private readonly DateTime? _currentTime;

        public ConvFormatterQwen2(DateTime currentTime)
        {
            _currentTime = currentTime;
        }

        public string FormatConversation(IEnumerable<Message> messages, bool addResponsePrimer)
        {
            var llmInput = new StringBuilder();

            foreach (Message message in messages)
            {
                llmInput.Append(FormatMessage(message.Author == Author.User ? "user" : "assistant", message.Content, message.Timestamp));
            }

            // reply primer
            if (addResponsePrimer)
            {
                llmInput.Append($"<|im_start|>assistant{FormatTimestamp(_currentTime)}\n");
            }

            return llmInput.ToString();
        }

        private static string FormatMessage(string author, string text, DateTime? timestamp)
        {
            return $"<|im_start|>{author}{FormatTimestamp(timestamp)}\n{text}<|im_end|>\n";
        }

        private static string FormatTimestamp(DateTime? timestamp)
        {
            if (timestamp.HasValue)
            {
                return " at " + timestamp.Value.ToString("yyyy-MM-dd HH:mm:ssZ (ddd)", CultureInfo.InvariantCulture);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
