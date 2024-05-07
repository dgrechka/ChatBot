using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    internal interface ILLMConfigFactory
    {
        Task<PromptConfig> CreateLLMConfig(Consumer consumer);
    }
}
