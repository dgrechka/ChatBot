using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot.LLMs.HuggingFace
{
    public class Llama3_8B : ITextGenerationLLM, IDisposable
    {
        class HuggingFaceReply
        {
            [JsonPropertyName("generated_text")]
            public string? GeneratedText { get; set; }
        }

        public TextCompletionModels Model => TextCompletionModels.Llama3_8B_instruct;

        private readonly HttpClient _httpClient;

        public string PromptFormatIdentifier => "llama3";

        public string[] DefaultStopStrings => new string[] { "<|eot_id|>" };

        public Llama3_8B(string huggingFaceApiKey)
        {
            // create http client with base address and authorization bearer token
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri("https://api-inference.huggingface.co/models/"),
                DefaultRequestHeaders =
                {
                    { "Authorization", $"Bearer {huggingFaceApiKey}" }
                }
            };
        }

        public async Task<string> GenerateResponseAsync(string prompt, AccountingInfo? accountingInfo, LLMCallSettings? settings, CancellationToken cancellationToken)
        {
            // do a POST request with llmInput.ToString() as the input
            JsonContent content = JsonContent.Create(new { inputs = prompt });
            HttpResponseMessage response = await _httpClient.PostAsync("meta-llama/Meta-Llama-3-8B-Instruct", content: content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to generate response: {response.ReasonPhrase}");
            }
            string responseContent = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<HuggingFaceReply[]>(responseContent);
            string generatedText = responseJson?[0]?.GeneratedText ?? string.Empty;
            if (!generatedText.StartsWith(prompt))
            {
                throw new Exception($"Unexpected response: {generatedText}");
            }

            string responseText = generatedText.Substring(prompt.Length);
            return responseText;

        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
