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

        public Task<IEnumerable<Message>> GetMessages(Chat chat, CancellationToken cancellationToken)
        {
            if (_storage.ContainsKey(chat))
            {
                return Task.FromResult<IEnumerable<Message>>(_storage[chat]);
            }
            else
            {
                return Task.FromResult<IEnumerable<Message>>(new Message[0]);
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
