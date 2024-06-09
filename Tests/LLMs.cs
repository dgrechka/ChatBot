using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatBot.LLMs;
using Moq;

namespace LLMs
{
    public class Llama3
    {
        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void Input_Generated_Without_Ts()
        {
            var config = new PromptConfig
            {
                BotPersonaSpecificPrompt = "Hello",
                UserSpecificPrompt = "Hi",
                IncludeMessageTimestamps = false
            };

            var messages = new List<Message>
            {
                new Message { Author = Author.User, Content = "How are you?" },
                new Message { Author = Author.Bot, Content = "I'm fine" },
                new Message { Author = Author.User, Content = "See you"}
            };

            var input = ChatBot.LLMs.Llama3ConvFormatter.PrepareInput(config, messages);

            Assert.Equal("<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\nHello\tHi<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nHow are you?<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\nI'm fine<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nSee you<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n", input);
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void Input_Generated_With_Ts()
        {
            var configMock = new Mock<IPromptConfig>();
            configMock.Setup(configMock => configMock.BotPersonaSpecificPrompt).Returns("Hello");
            configMock.Setup(configMock => configMock.UserSpecificPrompt).Returns("Hi");
            configMock.Setup(configMock => configMock.IncludeMessageTimestamps).Returns(true);
            configMock.Setup(configMock => configMock.CurrentTime).Returns(new DateTime(2024,06,05, 14,05,05));


            var messages = new List<Message>
            {
                new Message { Author = Author.User, Content = "How are you?", Timestamp = new DateTime(2024,06,05, 14,04,04) },
                new Message { Author = Author.Bot, Content = "I'm fine", Timestamp = new DateTime(2024,06,05, 14,04,05) },
                new Message { Author = Author.User, Content = "See you", Timestamp = new DateTime(2024,06,05, 14,05,04)}
            };

            var input = ChatBot.LLMs.Llama3ConvFormatter.PrepareInput(configMock.Object, messages);

            Assert.Equal(@"<|begin_of_text|><|start_header_id|>system<|end_header_id|>

Hello	Hi<|eot_id|><|start_header_id|>2024-06-05 14:04:04Z Wed
user<|end_header_id|>

How are you?<|eot_id|><|start_header_id|>2024-06-05 14:04:05Z Wed
assistant<|end_header_id|>

I'm fine<|eot_id|><|start_header_id|>2024-06-05 14:05:04Z Wed
user<|end_header_id|>

See you<|eot_id|><|start_header_id|>2024-06-05 14:05:05Z Wed
assistant<|end_header_id|>

".Replace("\r\n","\n"), input);
        }
    }
}
