using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public class Llama3_8B : ILLM, IDisposable
    {
        class HuggingFaceReply
        {
            [JsonPropertyName("generated_text")]
            public string GeneratedText { get; set; }
        }

        private readonly HttpClient _httpClient;

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

        public async Task<string> GenerateResponseAsync(IPromptConfig config, IEnumerable<Message> messages)
        {
            StringBuilder llmInput = new StringBuilder("<|begin_of_text|>");

            string systemPrompt = $"{config.BotPersonaSpecificPrompt}\t{config.UserSpecificPrompt}";

            llmInput.Append(FormatMessage("system", systemPrompt));
            foreach (Message message in messages)
            {
                llmInput.Append(FormatMessage(message.Author == Author.User ? "user" : "assistant", message.Content));
            }

            // reply primer
            llmInput.Append("<|start_header_id|>assistant<|end_header_id|>\n\n");

            // do a POST request with llmInput.ToString() as the input
            JsonContent content = JsonContent.Create(new { inputs = llmInput.ToString() });
            HttpResponseMessage response = await _httpClient.PostAsync("meta-llama/Meta-Llama-3-8B-Instruct", content:content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to generate response: {response.ReasonPhrase}");
            }
            string responseContent = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<HuggingFaceReply[]>(responseContent);
            string generatedText = responseJson[0].GeneratedText;

            string responseText = generatedText.Substring(llmInput.Length);
            return responseText;

        }

        private static string FormatMessage(string author, string text) => $"<|start_header_id|>{author}<|end_header_id|>\n\n{text}<|eot_id|>";

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
