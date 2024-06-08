using ChatBot.Prompt;
using Xunit;
using Moq;
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

        [Fact]
        [Trait(Attributes.TestCategory, Attributes.UnitTestCategory)]
        public async Task Compiler_Graph1()
        {
            var templateSourceMock = new Mock<ITemplateSource>();
            templateSourceMock.Setup(x => x.HasKey("abc")).ReturnsAsync(true);
            templateSourceMock.Setup(x => x.HasKey("de")).ReturnsAsync(true);
            templateSourceMock.Setup(x => x.HasKey("fghi")).ReturnsAsync(true);
            templateSourceMock.Setup(x => x.HasKey("jkl")).ReturnsAsync(true);

            templateSourceMock.Setup(x => x.GetValue("abc")).ReturnsAsync("Abc references [◄de►] and [◄fghi►].");
            templateSourceMock.Setup(x => x.GetValue("de")).ReturnsAsync("De references |◄fghi►|.");
            templateSourceMock.Setup(x => x.GetValue("fghi")).ReturnsAsync("Fghi references (◄jkl►).");
            templateSourceMock.Setup(x => x.GetValue("jkl")).ReturnsAsync("Jkl references nothing.");


            var compiler = new ChatBot.Prompt.Compiler(null, new List<ITemplateSource> {templateSourceMock.Object});

            var result = await compiler.CompilePrompt("abc");

            Assert.Equal("Abc references [De references |Fghi references (Jkl references nothing.).|.] and [Fghi references (Jkl references nothing.).].", result);
        }
    }
}
