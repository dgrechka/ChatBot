using ChatBot.Billing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot.LLMs.DeepInfra
{
    public class TextGenerationClient : InferenceClient<TextGenerationClient.InferenceRequest, TextGenerationClient.InferenceResponse>, ITextGenerationLLM
    {
        protected readonly IBillingLogger? _billingLogger;
        protected readonly TextCompletionModels _flavor;
        public TextGenerationClient(
            ILogger<ITextGenerationLLM>? logger,
            IBillingLogger? billingLogger,
            string apikey,
            int maxTokensToGenerate,
            TextCompletionModels flavor = TextCompletionModels.Llama3_8B_instruct)
            : base(logger, new DeepInfraInferenceClientSettings()
            {
                ApiKey = apikey,
                ModelName = flavor switch
                {
                    TextCompletionModels.Llama3_8B_instruct => "meta-llama/Meta-Llama-3-8B-Instruct",
                    TextCompletionModels.Llama3_70B_instruct => "meta-llama/Meta-Llama-3-70B-Instruct",
                    TextCompletionModels.Qwen2_72B_instruct => "Qwen/Qwen2-72B-Instruct",
                    _ => throw new ArgumentException("Invalid Llama3 flavor", nameof(flavor))
                },
                MaxTokensToGenerate = maxTokensToGenerate
            })
        {
            _billingLogger = billingLogger;
            _flavor = flavor;
        }

        public TextCompletionModels Model => _flavor;

        public string PromptFormatIdentifier
        {
            get
            {
                return _flavor switch
                {
                    TextCompletionModels.Llama3_8B_instruct => "llama3",
                    TextCompletionModels.Llama3_70B_instruct => "llama3",
                    TextCompletionModels.Qwen2_72B_instruct => "qwen2",
                    _ => throw new ArgumentException("Unsupported model", nameof(_flavor))
                };
            }
        }

        public string[] DefaultStopStrings {
            get
            {
                switch (_flavor) {
                    case TextCompletionModels.Llama3_8B_instruct:
                    case TextCompletionModels.Llama3_70B_instruct:
                        return new string[] { "<|eot_id|>" };
                    case TextCompletionModels.Qwen2_72B_instruct:
                        return new string[] { "\"<|im_start|>", "<|im_end|>", "</s>" };
                    default:
                        throw new ArgumentException("Unsupported model", nameof(_flavor));
                }
            }
        }

        public class ResponseFormat {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "json_object";
        }

        public class InferenceRequest
        {
            [JsonPropertyName("input")]
            public string Input { get; set; } = string.Empty;

            [JsonPropertyName("max_new_tokens")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? MaxNewTokens { get; set; } = null;

            [JsonPropertyName("stop")]
            public string[] Stop { get; set; } = new string[] { "<|eot_id|>" };

            [JsonPropertyName("response_format")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public ResponseFormat? ResponseFormat { get; set; }

            [JsonPropertyName("temperature")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public double? Temperature { get; set; } = 0.7;

        }

        public class InferenceResponse
        {
            [JsonPropertyName("request_id")]
            public string? RequestId { get; set; }

            [JsonPropertyName("inference_status")]
            public InferenceStatus? Status { get; set; }

            [JsonPropertyName("results")]
            public Results[]? Results { get; set; }
        }

        public class Results
        {
            [JsonPropertyName("generated_text")]
            public string? GeneratedText { get; set; }
        }

        public async Task<string> GenerateResponseAsync(string prompt, AccountingInfo? accountingInfo, LLMCallSettings? callSettings, CancellationToken cancellationToken)
        {
            var request = new InferenceRequest
            {
                Input = prompt,
                MaxNewTokens = _settings.MaxTokensToGenerate,
            };

            if (callSettings?.StopStrings != null)
            {
                request.Stop = callSettings.StopStrings.ToArray();
            }

            if(callSettings?.ProduceJSON != null)
            {
                request.ResponseFormat = new ResponseFormat() { Type = "json_object" };
            }

            if (callSettings?.Temperature != null)
            {
                request.Temperature = callSettings.Temperature;
            }

            var response = await CallLLM(request, cancellationToken);
            _logger?.LogInformation($"Got LLM response. RuntimeMs: {response?.Status?.RuntimeMs}");

            if (accountingInfo != null && _billingLogger != null)
            {
                _billingLogger?.LogLLMCost(accountingInfo, "DeepInfra", _settings.ModelName, response?.Status?.TokensInput ?? 0, response?.Status?.TokensGenerated ?? 0, response?.Status?.Cost ?? 0, "USD", cancellationToken);
            }

            return response?.Results?[0]?.GeneratedText ?? string.Empty;
        }
    }



}
