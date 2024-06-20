using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ChatBot.Billing;

namespace ChatBot.LLMs.DeepInfra
{
    public class TextEmbeddingClient : InferenceClient<TextEmbeddingClient.InferenceRequest, TextEmbeddingClient.InferenceResponse>, ITextEmbeddingLLM
    {
        protected readonly IBillingLogger? _billingLogger;

        public TextEmbeddingClient(
            ILogger<ITextEmbeddingLLM> logger,
            IBillingLogger? billingLogger,
            DeepInfraInferenceClientSettings settings)
            : base(logger, settings)
            {
                _billingLogger = billingLogger;
            }

        public async Task<double[]> GenerateEmbeddingAsync(string text, AccountingInfo? accountingInfo, CancellationToken cancellationToken)
        {
            var response = await this.CallLLM(new InferenceRequest { Texts = [text], Normalize=true }, cancellationToken);
            if(response?.Status?.Status != StatusEnum.Succeeded)
            {
                throw new Exception($"Failed to generate embedding: {response?.Status}");
            }
            if (accountingInfo != null && _billingLogger != null)
            {
                // Log the cost of the LLM call
                _ = _billingLogger.LogLLMCost(accountingInfo,
                    "DeepInfra",
                    _settings.ModelName,
                    response?.Status?.TokensInput ?? 0,
                    0,
                    response?.Status?.Cost ?? 0,
                    "USD",
                    cancellationToken);
            }

            if (response?.Embeddings?.Length != 1)
            {
                throw new Exception($"Unexpected number of embeddings: {response?.Embeddings?.Length ?? 0}");
            }

            _logger?.LogInformation($"DeepInfraInferenceClient[{_settings.ModelName}]: Generated embedding for text length {text.Length}, resulting embedding dimensions: {response?.Embeddings[0]?.Length ?? 0}");
            
            return response!.Embeddings[0];
        }

        public TextEmbeddingModels Model => _settings.ModelName switch
        {
            "BAAI/bge-m3" => TextEmbeddingModels.BGE_M3,
            _ => throw new ArgumentException("Invalid embedding model", nameof(_settings.ModelName))
        };

        public int EmbeddingDimensionsCount => this.Model switch
        {
            TextEmbeddingModels.BGE_M3 => 1024,
            _ => throw new ArgumentException("Invalid embedding model", nameof(this.Model))
        };

        public class InferenceRequest
        {
            [JsonPropertyName("inputs")]
            public string[] Texts { get; set; } = Array.Empty<string>();

            [JsonPropertyName("normalize")]
            public bool Normalize { get; set; }
        }

        public class InferenceResponse
        {
            [JsonPropertyName("embeddings")]
            public double[][]? Embeddings { get; set; }

            [JsonPropertyName("inference_status")]
            public InferenceStatus? Status { get; set; }
        }
    }
}
