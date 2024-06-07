using ChatBot.Prompt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prompt
{
    public class Template
    {
        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void Templating()
        {
            var template = new ChatBot.Prompt.Template("Hello, ◄name►! I'm ◄bot_name►.");

            Assert.Contains("name", template.Placeholders);
            Assert.Contains("bot_name", template.Placeholders);
            Assert.Equal(2, template.Placeholders.Count);
            Assert.Equal("Hello, John! I'm ChatBot.", template.Render(new Dictionary<string, string>() {
                { "name", "John" },
                { "bot_name", "ChatBot" }
            }));
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void PlaceholderInTheBeginning()
        {
            var template = new ChatBot.Prompt.Template("◄name►! I'm ◄bot_name►.");

            Assert.Contains("name", template.Placeholders);
            Assert.Contains("bot_name", template.Placeholders);
            Assert.Equal(2, template.Placeholders.Count);
            Assert.Equal("John! I'm ChatBot.", template.Render(new Dictionary<string, string>() {
                { "name", "John" },
                { "bot_name", "ChatBot" }
            }));
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void PlaceholderInTheEnd()
        {
            var template = new ChatBot.Prompt.Template("Hello, ◄name►! I'm ◄bot_name►");

            Assert.Contains("name", template.Placeholders);
            Assert.Contains("bot_name", template.Placeholders);
            Assert.Equal(2, template.Placeholders.Count);
            Assert.Equal("Hello, John! I'm ChatBot", template.Render(new Dictionary<string, string>() {
                { "name", "John" },
                { "bot_name", "ChatBot" }
            }));
        }

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public void NoPlaceholders()
        {
            var template = new ChatBot.Prompt.Template("Hello, Bill! I'm Bot");

            Assert.Empty(template.Placeholders);
            Assert.Equal("Hello, Bill! I'm Bot", template.Render(new Dictionary<string, string>() {
                { "name", "John" },
                { "bot_name", "ChatBot" }
            }));
        }
    }
}
