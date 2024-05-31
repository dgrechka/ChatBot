using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public static class Llama3
    {
        public static string PrepareInput(IPromptConfig config, IEnumerable<Message> messages, bool addResponsePrimer = true)
        {
            StringBuilder llmInput = new StringBuilder("<|begin_of_text|>");

            string systemPrompt = $"{config.BotPersonaSpecificPrompt}\t{config.UserSpecificPrompt}";

            llmInput.Append(FormatMessage("system", systemPrompt));
            foreach (Message message in messages)
            {
                llmInput.Append(FormatMessage(message.Author == Author.User ? "user" : "assistant", message.Content));
            }

            // reply primer
            if(addResponsePrimer)
                llmInput.Append("<|start_header_id|>assistant<|end_header_id|>\n\n");

            return llmInput.ToString();
        }

        private static string FormatMessage(string author, string text) => $"<|start_header_id|>{author}<|end_header_id|>\n\n{text}<|eot_id|>";
    }
}
