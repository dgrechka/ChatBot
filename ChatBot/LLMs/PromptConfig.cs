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

        bool IncludeMessageTimestamps { get; }

        Chat Chat { get; }

        DateTime? CurrentTime { get; }
    }

    public class PromptConfig : IPromptConfig
    {
        public string BotPersonaSpecificPrompt { get; set; }

        public string UserSpecificPrompt { get; set; }

        public Chat Chat { get; set; }

        public bool IncludeMessageTimestamps { get; set; }

        public DateTime? CurrentTime => IncludeMessageTimestamps ? DateTime.UtcNow : null;
    }
}
