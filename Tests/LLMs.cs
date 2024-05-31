using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatBot.LLMs;

namespace LLMs
{
    public class Llama3
    {
        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void Input_Generated()
        {
            var config = new PromptConfig
            {
                BotPersonaSpecificPrompt = "Hello",
                UserSpecificPrompt = "Hi"
            };

            var messages = new List<Message>
            {
                new Message { Author = Author.User, Content = "How are you?" },
                new Message { Author = Author.Bot, Content = "I'm fine" },
                new Message { Author = Author.User, Content = "See you"}
            };

            var input = ChatBot.LLMs.Llama3.PrepareInput(config, messages);

            Assert.Equal("<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\nHello\tHi<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nHow are you?<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\nI'm fine<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nSee you<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n", input);
        }
    }
}
