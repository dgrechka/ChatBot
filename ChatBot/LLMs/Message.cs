using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public enum Author
    {
        User,
        Bot
    }

    public class Message
    {
        public DateTime Timestamp { get; set; }
        public Author Author { get; set; }
        public string Content { get; set; }
    }
}
