using ChatBot;
using Moq;
using DI = ChatBot.LLMs.DeepInfra;

namespace DeepInfra
{
    public class Llama3_8B
    {
        private readonly string _deepinfra_apikey;

        public Llama3_8B()
        {
            _deepinfra_apikey = Environment.GetEnvironmentVariable("DEEPINFRA_APIKEY");
            if (string.IsNullOrWhiteSpace(_deepinfra_apikey))
            {
                throw new Exception("DEEPINFRA_APIKEY env var is not set");
            }
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.IntegrationTestCategory)]
        [Trait(Attributes.RequiresApiKeys,"true")]
        public async Task ResponseGenerated()
        {
            using DI.TextGenerationClient llama3_8B = new DI.TextGenerationClient(null, null, _deepinfra_apikey, 32, TextCompletionModels.Llama3_8B_instruct);

            var prompt = @"<|begin_of_text|><|start_header_id|>system<|end_header_id|>

Your name is Donald. You must reply with single word.<|eot_id|><|start_header_id|>user<|end_header_id|>

Hello<|eot_id|><|start_header_id|>assistant<|end_header_id|>

Hi<|eot_id|><|start_header_id|>user<|end_header_id|>

What's your name?<|eot_id|><|start_header_id|>assistant<|end_header_id|>

";


            var response = await llama3_8B.GenerateResponseAsync(prompt, null, null, CancellationToken.None);

            Assert.Equal("Donald", response);
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.IntegrationTestCategory)]
        [Trait(Attributes.RequiresApiKeys, "true")]
        public async Task PriorMessageIsInContext()
        {
            using DI.TextGenerationClient llama3_8B = new DI.TextGenerationClient(null, null, _deepinfra_apikey, 32, TextCompletionModels.Llama3_8B_instruct);

            var prompt = @"<|begin_of_text|><|start_header_id|>system<|end_header_id|>

Your name is Donald. You must reply with single word.<|eot_id|><|start_header_id|>user<|end_header_id|>

Hello<|eot_id|><|start_header_id|>assistant<|end_header_id|>

Hi<|eot_id|><|start_header_id|>user<|end_header_id|>

The length of AB is 3241<|eot_id|><|start_header_id|>assistant<|end_header_id|>

Ok<|eot_id|><|start_header_id|>user<|end_header_id|>

What is the length of AB?<|eot_id|><|start_header_id|>assistant<|end_header_id|>

";

            var response = await llama3_8B.GenerateResponseAsync(prompt, null, null, CancellationToken.None);

            Assert.Equal("3241", response);
        }
    }
}