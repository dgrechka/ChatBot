using ChatBot.LLMs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Processing.ChatTurn
{
    public interface IChatAuthorization {
        Task<bool> IsAuthorized();
    }

    public class PromptDefinedAuthorizationScoped : IChatAuthorization
    {
        private readonly ILogger<IChatAuthorization>? _logger;
        private readonly UserMessageContext _context;
        private readonly HashSet<Chat> authorizedChats = new ();

        public PromptDefinedAuthorizationScoped(
            ILogger<IChatAuthorization>? logger,
            UserMessageContext context,
            Dictionary<string,string> promptDicts,
            string prefix)
        {
            _context = context;
            _logger = logger;

            promptDicts.Keys.ToList().ForEach(key => {
                if (key.StartsWith(prefix))
                {
                    authorizedChats.Add(new Chat(key.Substring(prefix.Length)));
                }
            });
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<bool> IsAuthorized()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (_context.Chat == null)
            {
                throw new InvalidOperationException("Chat is not set in the context");
            }
            var res = authorizedChats.Contains(_context.Chat);
            _logger?.LogInformation("Chat {chat} is authorized: {res}", _context.Chat, res);
            return res;
        }
    }
}
