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

    public class CallSettings {
        public List<string> StopStrings { get; set; } = new();
    }

    public interface ILLM
    {
        Task<string> GenerateResponseAsync(string prompt, AccountingInfo? accountingInfo, CallSettings? callSettings, CancellationToken cancellationToken);
    }
}
