using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatBot.LLMs;
using Microsoft.VisualBasic;

namespace ChatBot.Processing.ScheduledTasks
{
    public class Helpers
    {
        public static async IAsyncEnumerable<List<Message>> ClusterConversations(IAsyncEnumerable<Message> messages, TimeSpan idleConversationTime)
        {
            // then we cluster them based on their timestamp
            List<Message> conversation = new();
            DateTime? prevMessageTime = null;

            var now = DateTime.UtcNow;

            await foreach (var message in messages)
            {
                if (now - message.Timestamp < idleConversationTime)
                {
                    // the conversation is still ongoing. will not process it yet
                    yield break;
                }

                if (conversation.Count != 0 && message.Timestamp - prevMessageTime > idleConversationTime)
                {
                    // process the conversation
                    yield return conversation;
                    conversation = new();
                }

                conversation.Add(message);
                prevMessageTime = message.Timestamp;
            }

            if (conversation.Count != 0)
            {
                yield return conversation;
            }
        }

        public static string FormatConversation(IEnumerable<Message> conversation, out DateTime? firstMessageTime, out DateTime? lastMessageTime, out int messageCounter)
        {
            StringBuilder convSB = new();
            bool first = true;
            firstMessageTime = null;
            lastMessageTime = null;
            messageCounter = 0;
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
                messageCounter++;
            }

            convSB.AppendLine($"[end of conversation at {FormatTimestamp(lastMessageTime) ?? string.Empty}]");

            return convSB.ToString();
        }


        public static string FormatTimestamp(DateTime? timestamp)
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
