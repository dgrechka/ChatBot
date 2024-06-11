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
        Task<IEnumerable<Message>> GetMessages(Chat chat, CancellationToken cancellationToken);
    }

    public interface IChatHistoryWriter
    {
        Task LogMessages(Chat chat, IEnumerable<Message> messages, CancellationToken cancellationToken);
    }
}
