using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Interfaces
{
    public interface IChatHistoryReader
    {
        IAsyncEnumerable<Message> GetMessagesSince(Chat chat, DateTime time, CancellationToken cancellationToken);

        IAsyncEnumerable<(Chat,DateTime)> GetChatsLastMessageTime(CancellationToken cancellationToken);
    }

    public interface IChatHistoryWriter
    {
        Task LogMessages(Chat chat, IEnumerable<Message> messages, CancellationToken cancellationToken);
    }
}
