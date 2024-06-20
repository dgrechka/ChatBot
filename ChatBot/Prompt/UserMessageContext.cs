using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class UserMessageContext
    {
        public Message? Message { get; set; }
        public Chat? Chat { get; set; }
        public TextCompletionModels? ActiveModel { get; set; }
    }
}
