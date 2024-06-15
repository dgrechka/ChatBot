using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Diagnostics;
using ChatBot.Billing;

namespace ChatBot.LLMs.DeepInfra
{
    public abstract class InferenceClient<Request, Response> : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool disposedValue;

        protected readonly ILogger? _logger;
        protected readonly DeepInfraInferenceClientSettings _settings;

        public InferenceClient(ILogger? logger, DeepInfraInferenceClientSettings settings)
        {
            _logger = logger;
            _settings = settings;
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri($"https://api.deepinfra.com/v1/inference/{settings.ModelName}"),
                DefaultRequestHeaders =
                {
                    { "Authorization", $"Bearer {settings.ApiKey}" }
                }
            };
        }

        public async Task<Response> GenerateResponseAsync(Request request, CancellationToken cancellationToken)
        {
            // do a POST request with request as the input
            JsonContent content = JsonContent.Create(request);
            _logger?.LogInformation($"DeepInfraInferenceClient[{_settings.ModelName}]: Sending request. {_settings.MaxTokensToGenerate} max tokens to generate.");
            var sw = Stopwatch.StartNew();
            HttpResponseMessage response = await _httpClient.PostAsync("", content: content, cancellationToken);
            sw.Stop();
            _logger?.LogInformation($"DeepInfraInferenceClient[{_settings.ModelName}]: Request took {sw.ElapsedMilliseconds}ms");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to generate response: {response.ReasonPhrase}");
            }
            string responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Response>(responseContent);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _httpClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class DeepInfraInferenceClientSettings
    {
        public string ModelName { get; set; }
        public string ApiKey { get; set; }

        public int MaxTokensToGenerate { get; set; } = 512;
    }
}
