using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace ChatBot.LLMs
{
    public class AccountingInfo
    {
        public Chat Chat { get; }
        public string CallPurpose { get; }

        public AccountingInfo(Chat chat, string callPurpose)
        {
            Chat = chat;
            CallPurpose = callPurpose;
        }
    }

    public class LLMCallSettings {
        public List<string> StopStrings { get; set; } = new();
        public double? Temperature { get; set; }
        public bool? ProduceJSON { get; set; }
    }

    public interface ITextGenerationLLM
    {
        Task<string> GenerateResponseAsync(string prompt, AccountingInfo? accountingInfo, LLMCallSettings? callSettings, CancellationToken cancellationToken);

        public TextCompletionModels Model { get; }

        /// <summary>
        /// Can be used as a part of placeholder to distinguish different prompt formats required for different models (e.g. llama3, qwen2, etc.)
        /// </summary>
        public string PromptFormatIdentifier { get; }

        public string[] DefaultStopStrings { get; }
    }
}
