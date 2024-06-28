using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class LlmInfoTemplateSourceScoped : ITemplateSource
    {
        private const string TemplateKey = "chat-llm";

        private readonly ITextGenerationLLMFactory _textGenerationLLMFactory;

        public LlmInfoTemplateSourceScoped(ITextGenerationLLMFactory textGenerationLLMFactory)
        {
            _textGenerationLLMFactory = textGenerationLLMFactory;
        }

        public Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key != TemplateKey)
            {
                throw new ArgumentException($"Unexpected key: {key}");
            }

            var llm = _textGenerationLLMFactory.CreateLLM(TextGenerationLLMRole.ChatTurn);
            return Task.FromResult(llm.Model.ToString());
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == TemplateKey);
        }
    }
}
