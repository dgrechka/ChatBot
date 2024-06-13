using ChatBot.Interfaces;
using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Chats
{
    public class MemoryChatHistory : IChatHistoryReader, IChatHistoryWriter
    {
        private readonly Dictionary<Chat, LinkedList<Message>> _storage = new Dictionary<Chat, LinkedList<Message>>();

        public async IAsyncEnumerable<Message> GetMessagesSince(Chat chat, DateTime time, CancellationToken cancellationToken)
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

        public async IAsyncEnumerable<Chat> GetChats(CancellationToken cancellationToken)
        {
            foreach (var chat in _storage.Keys)
            {
                yield return chat;
            }
        }

        public Task LogMessages(Chat chat, IEnumerable<Message> newMessages, CancellationToken cancellationToken)
        {
            LinkedList<Message> messages;
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
    }
}
