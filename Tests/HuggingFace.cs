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

            var configMock = new Mock<ChatBot.LLMs.IPromptConfig>();
            configMock.SetupGet(x => x.BotPersonaSpecificPrompt).Returns("My name is Donald.");
            configMock.SetupGet(x => x.UserSpecificPrompt).Returns("I must reply with single word.");


            var response = await llama3_8B.GenerateResponseAsync(configMock.Object, new List<ChatBot.LLMs.Message>
            {
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.User, Content = "Hello" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.Bot, Content = "Hi" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.User, Content = "What's your name?" }
            });

            Assert.Equal("Donald", response);
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.IntegrationTestCategory)]
        [Trait(Attributes.RequiresApiKeys, "true")]
        public async Task PriorMessageIsInContext()
        {
            using HF.Llama3_8B llama3_8B = new HF.Llama3_8B(_huggingFaceToken);

            var configMock = new Mock<ChatBot.LLMs.IPromptConfig>();
            configMock.SetupGet(x => x.BotPersonaSpecificPrompt).Returns("My name is Donald.");
            configMock.SetupGet(x => x.UserSpecificPrompt).Returns("I must reply with single word.");

            var response = await llama3_8B.GenerateResponseAsync(configMock.Object, new List<ChatBot.LLMs.Message>
            {
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.User, Content = "Hello" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.Bot, Content = "Hi" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.User, Content = "The length of AB is 3241" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.Bot, Content = "Ok" },
                new ChatBot.LLMs.Message { Author = ChatBot.LLMs.Author.User, Content = "What is the length of AB?" },
            });

            Assert.Equal("3241", response);
        }
    }
}