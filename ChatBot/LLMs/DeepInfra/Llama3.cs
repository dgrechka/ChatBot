﻿using ChatBot.Billing;
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
            TextCompletionModels flavor = TextCompletionModels.Llama3_8B_instruct)
            : base(logger,new DeepInfraInferenceClientSettings() {
                ApiKey = apikey,
                ModelName = flavor switch
                {
                    TextCompletionModels.Llama3_8B_instruct => "meta-llama/Meta-Llama-3-8B-Instruct",
                    TextCompletionModels.Llama3_70B_instruct => "meta-llama/Meta-Llama-3-70B-Instruct",
                    TextCompletionModels.Qwen2_72B_instruct => "Qwen/Qwen2-72B-Instruct",
                    _ => throw new ArgumentException("Invalid Llama3 flavor", nameof(flavor))
                }
            })
        {
            _billingLogger = billingLogger;
            _flavor = flavor;
        }

        public TextCompletionModels Model => _flavor;

        public class InferenceRequest
        {
            [JsonPropertyName("input")]
            public string Input { get; set; }
            
            [JsonPropertyName("max_new_tokens")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? MaxNewTokens { get; set; } = null;

            [JsonPropertyName("stop")]
            public string[] Stop { get; set; } = new string[] { "<|eot_id|>" };

            [JsonPropertyName("temperature")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public double? Temperature { get; set; } = 0.7;

        }

        public class InferenceResponse
        {
            [JsonPropertyName("request_id")]
            public string RequestId { get; set; }

            [JsonPropertyName("inference_status")]
            public InferenceStatus Status { get; set; }

            [JsonPropertyName("results")]
            public Results[] Results { get; set; }
        }

        public class Results
        {
            [JsonPropertyName("generated_text")]
            public string GeneratedText { get; set; }
        }

        public enum StatusEnum { Unknown, Queued, Running, Succeeded, Failed }

        public class InferenceStatus
        {
            [JsonIgnore]
            public StatusEnum Status { get; set; } = StatusEnum.Unknown;

            [JsonPropertyName("status")]
            public string StatusString
            {
                get => Status.ToString().ToLower();
                set => Status = Enum.Parse<StatusEnum>(value, ignoreCase: true);
            }

            [JsonPropertyName("runtime_ms")]
            public int RuntimeMs { get; set; }

            [JsonPropertyName("cost")]
            public decimal Cost { get; set; }

            [JsonPropertyName("tokens_input")]
            public int TokensInput { get; set; }

            [JsonPropertyName("tokens_generated")]
            public int TokensGenerated { get; set; }

        }

        public async Task<string> GenerateResponseAsync(string prompt, AccountingInfo? accountingInfo, CallSettings? callSettings, CancellationToken cancellationToken)
        {
            var request = new InferenceRequest
            {
                Input = prompt,
                MaxNewTokens = _settings.MaxTokens,
            };

            if(callSettings?.StopStrings != null)
            {
                request.Stop = callSettings.StopStrings.ToArray();
            }

            if(callSettings?.Temperature != null)
            {
                request.Temperature = callSettings.Temperature;
            }

            var response = await GenerateResponseAsync(request, cancellationToken);
            _logger?.LogInformation($"InputTokens: {response.Status.TokensInput}; OutputTokens: {response.Status.TokensGenerated}; Cost: {response.Status.Cost}; RuntimeMs: {response.Status.RuntimeMs}");

            _billingLogger?.LogLLMCost(accountingInfo, "DeepInfra", _settings.ModelName, response.Status.TokensInput, response.Status.TokensGenerated, response.Status.Cost, "USD", cancellationToken);

            return response.Results[0].GeneratedText;
        }
    }



}
