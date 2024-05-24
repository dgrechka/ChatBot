using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public interface IPromptConfig
    {
        string BotPersonaSpecificPrompt { get;}

        string UserSpecificPrompt { get;}
    }

    public class PromptConfig : IPromptConfig
    {
        public string BotPersonaSpecificPrompt { get; set; }

        public string UserSpecificPrompt { get; set; }
    }
}
