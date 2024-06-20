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
        //[Fact]
        //[Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        //public void Input_Generated_Without_Ts()
        //{
        //    var messages = new List<Message>
        //    {
        //        new Message { Author = Author.User, Content = "How are you?" },
        //        new Message { Author = Author.Bot, Content = "I'm fine" },
        //        new Message { Author = Author.User, Content = "See you"}
        //    };

        //    var formatter = new ChatBot.Prompt.ConvFormatterLlama3(null);

        //    var input = formatter.FormatConversation(messages, true);

        //    Assert.Equal("<|start_header_id|>user<|end_header_id|>\n\nHow are you?<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\nI'm fine<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nSee you<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n", input);
        //}

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void Input_Generated_With_Ts()
        {
            var messages = new List<Message>
            {
                new Message(new DateTime(2024,06,05, 14,04,04),Author.User,"How are you?"),
                new Message(new DateTime(2024,06,05, 14,04,05),Author.Bot,"I'm fine?"),
                new Message(new DateTime(2024,06,05, 14,05,04),Author.User,"See you")
            };

            var formatter = new ChatBot.Prompt.ConvFormatterLlama3(new DateTime(2024, 06, 05, 14, 05, 05, DateTimeKind.Utc));

            var input = formatter.FormatConversation(messages, true);

            Assert.Equal(@"<|start_header_id|>user at 2024-06-05 14:04:04Z (Wed)<|end_header_id|>

How are you?<|eot_id|><|start_header_id|>assistant at 2024-06-05 14:04:05Z (Wed)<|end_header_id|>

I'm fine<|eot_id|><|start_header_id|>user at 2024-06-05 14:05:04Z (Wed)<|end_header_id|>

See you<|eot_id|><|start_header_id|>assistant at 2024-06-05 14:05:05Z (Wed)<|end_header_id|>

".Replace("\r\n","\n"), input);
        }
    }
}
