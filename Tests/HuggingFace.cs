using Moq;
using HF = ChatBot.LLMs.HuggingFace;

namespace HuggingFace
{
    public class Llama3_8B
    {
        private readonly string _huggingFaceToken;

        public Llama3_8B()
        {
            _huggingFaceToken = Environment.GetEnvironmentVariable("HUGGING_FACE_TOKEN");
            if (string.IsNullOrWhiteSpace(_huggingFaceToken))
            {
                throw new Exception("HUGGING_FACE_TOKEN env var is not set");
            }
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.IntegrationTestCategory)]
        [Trait(Attributes.RequiresApiKeys,"true")]
        public async Task ResponseGenerated()
        {
            using HF.Llama3_8B llama3_8B = new HF.Llama3_8B(_huggingFaceToken);

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
            using HF.Llama3_8B llama3_8B = new HF.Llama3_8B(_huggingFaceToken);

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