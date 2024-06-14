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
    public enum Llama3Flavor { Instruct_70B, Instruct_8B }

    public class Llama3Client : InferenceClient<Llama3Client.InferenceRequest, Llama3Client.InferenceResponse>, ILLM
    {
        protected readonly IBillingLogger? _billingLogger;
        public Llama3Client(
            ILogger<Llama3Client>? logger,
            IBillingLogger? billingLogger,
            string apikey,
            int maxTokens,
            Llama3Flavor flavor = Llama3Flavor.Instruct_8B)
            : base(logger,new DeepInfraInferenceClientSettings() {
                ApiKey = apikey,
                MaxTokens = maxTokens,
                ModelName = flavor switch
                {
                    Llama3Flavor.Instruct_8B => "meta-llama/Meta-Llama-3-8B-Instruct",
                    Llama3Flavor.Instruct_70B => "meta-llama/Meta-Llama-3-70B-Instruct",
                    _ => throw new ArgumentException("Invalid Llama3 flavor", nameof(flavor))
                }
            })
        {
            _billingLogger = billingLogger;
        }

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
