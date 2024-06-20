using ChatBot.Interfaces;
using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Chats
{
    public class MemoryChatHistory : IChatHistoryReader, IChatHistoryWriter
    {
        private readonly Dictionary<Chat, LinkedList<Message>> _storage = new Dictionary<Chat, LinkedList<Message>>();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<Message> GetMessagesSince(Chat chat, DateTime time, [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            IEnumerable<Message> messages;
            if (_storage.ContainsKey(chat))
            {
                messages =_storage[chat].Where(m => m.Timestamp > time).OrderBy(m => m.Timestamp);
            }
            else
            {
                messages = [];
            }

            foreach (var message in messages)
            {
                yield return message;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<Chat> GetChats([EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var chat in _storage.Keys)
            {
                yield return chat;
            }
        }

        public Task LogMessages(Chat chat, IEnumerable<Message> newMessages, CancellationToken cancellationToken)
        {
            LinkedList<Message>? messages;
            if (!_storage.TryGetValue(chat, out messages)) { 
                messages = new LinkedList<Message>();
                _storage.Add(chat, messages);
            }

            foreach (var message in newMessages)
            {
                messages.AddLast(message);
                if (messages.Count > 10)
                {
                    messages.RemoveFirst();
                }
            }

            return Task.CompletedTask;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<(Chat, DateTime)> GetChatsLastMessageTime([EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var (chat, messages) in _storage)
            {
                yield return (chat, messages.Last!.Value!.Timestamp);
            }
        }
    }
}
