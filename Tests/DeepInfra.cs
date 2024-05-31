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
            using DI.Llama3Client llama3_8B = new DI.Llama3Client(null, _deepinfra_apikey, 32, DI.Llama3Flavor.Instruct_8B);

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
            using DI.Llama3Client llama3_8B = new DI.Llama3Client(null, _deepinfra_apikey, 32, DI.Llama3Flavor.Instruct_8B);

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