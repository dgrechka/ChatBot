using ChatBot.Billing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public enum TextEmbeddingLLMRole
    {
        ConvSummary,
        CurrentConversation
    }

    public interface ITextEmbeddingLLMFactory
    {
        public ITextEmbeddingLLM CreateLLM(TextEmbeddingLLMRole role);
    }

    public class TextEmbeddingLLMFactory : ITextEmbeddingLLMFactory
    {
        private readonly ModelsConfig _config;
        private readonly Dictionary<string,DeepInfraModelProviderConfig> _modelProvidersConfigs;

        private readonly IBillingLogger? _billingLogger;
        private readonly ILogger<ITextEmbeddingLLM> _llmLogger;
        private readonly Dictionary<TextEmbeddingLLMRole, ITextEmbeddingLLM> cache = new();

        public TextEmbeddingLLMFactory(
            ModelsConfig config,
            Dictionary<string, DeepInfraModelProviderConfig> modelProviderConfigs,
            IBillingLogger? billingLogger,
            ILogger<ITextEmbeddingLLM> llmLogger)
        {
            _config = config;
            _modelProvidersConfigs = modelProviderConfigs;
            _billingLogger = billingLogger;
            _llmLogger = llmLogger;
        }

        public ITextEmbeddingLLM CreateLLM(TextEmbeddingLLMRole role)
        {
            if (cache.TryGetValue(role, out var llm))
            {
                return llm;
            }

            var modelConfig = role switch
            {
                TextEmbeddingLLMRole.ConvSummary => _config.ConvSummaryEmbedding,
                TextEmbeddingLLMRole.CurrentConversation => _config.ConvSummaryEmbedding,
                _ => throw new ArgumentException($"Role {role} is not supported")
            };

            if (modelConfig == null)
            {
                throw new ArgumentException($"Model config for role {role} is not set");
            }

            if(!_modelProvidersConfigs.TryGetValue(modelConfig.Provider, out var modelProviderConfig))
            {
                throw new ArgumentException($"Model provider {modelConfig.Provider} config for role {role} is not found in the configuration");
            }

            switch(modelProviderConfig)
            {
                case DeepInfraModelProviderConfig deepInfraModelProviderConfig:
                    cache[role] = new DeepInfra.TextEmbeddingClient(
                        _llmLogger,
                        _billingLogger,
                        new DeepInfra.DeepInfraInferenceClientSettings() {
                            ApiKey = deepInfraModelProviderConfig.ApiKey,
                            ModelName = modelConfig?.Model switch {
                                TextEmbeddingModels.BGE_M3 => "BAAI/bge-m3",
                                _ => throw new ArgumentException("Invalid embedding model", nameof(modelConfig.Model))
                            },
                            MaxTokensToGenerate = 0
                        }
                    );
                    break;
                default:
                    throw new ArgumentException($"Model provider {modelProviderConfig.Type} is not supported");
            }

            return cache[role];
        }
    }
}
