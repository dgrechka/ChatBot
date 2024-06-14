using ChatBot.Billing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public enum TextGenerationLLMRole
    {
        ChatTurn,
        ConvSummary,
        UserProfileUpdater
    }

    public interface ITextGenerationLLMFactory
    {
        public ITextGenerationLLM CreateLLM(TextGenerationLLMRole role);
    }

    public class TextGenerationLLMFactory : ITextGenerationLLMFactory
    {
        private readonly ModelsConfig _config;
        private readonly ModelProviderConfig[] _modelProvidersConfigs;

        private readonly IBillingLogger? _billingLogger;
        private readonly ILogger<ITextGenerationLLM> _llmLogger;
        private readonly Dictionary<TextGenerationLLMRole, ITextGenerationLLM> cache = new();

        public TextGenerationLLMFactory(
            ModelsConfig config,
            ModelProviderConfig[] modelProviderConfigs,
            IBillingLogger? billingLogger,
            ILogger<ITextGenerationLLM> llmLogger)
        {
            _config = config;
            _modelProvidersConfigs = modelProviderConfigs;
            _billingLogger = billingLogger;
            _llmLogger = llmLogger;
        }

        public ITextGenerationLLM CreateLLM(TextGenerationLLMRole role)
        {
            if (cache.TryGetValue(role, out var llm))
            {
                return llm;
            }

            var modelConfig = role switch
            {
                TextGenerationLLMRole.ChatTurn => _config.ChatTurn,
                TextGenerationLLMRole.ConvSummary => _config.ConvSummary,
                TextGenerationLLMRole.UserProfileUpdater => _config.UserProfileUpdater,
                _ => throw new ArgumentException($"Role {role} is not supported")
            };

            if (modelConfig == null)
            {
                throw new ArgumentException($"Model config for role {role} is not set");
            }

            var modelProviderConfig = _modelProvidersConfigs.FirstOrDefault(c => c.Name == modelConfig.Provider);
            if (modelProviderConfig == null)
            {
                throw new ArgumentException($"Model provider {modelConfig.Provider} config for role {role} is configured");
            }

            switch(modelProviderConfig)
            {
                case DeepInfraModelProviderConfig deepInfraModelProviderConfig:
                    cache[role] = new DeepInfra.TextGenerationClient(
                        _llmLogger,
                        _billingLogger,
                        deepInfraModelProviderConfig.ApiKey,
                        modelConfig.Model
                    );
                    break;
                default:
                    throw new ArgumentException($"Model provider {modelProviderConfig.Type} is not supported");
            }

            return cache[role];
        }
    }
}
