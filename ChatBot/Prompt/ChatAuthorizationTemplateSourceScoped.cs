using ChatBot.Processing.ChatTurn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class AuthorizationSettings {
        public string AuthorizedTemplate { get; set; } = string.Empty;
        public string UnauthorizedTemplate { get; set; } = string.Empty;
    }
    public class ChatAuthorizationTemplateSourceScoped : ITemplateSource
    {
        private static string _templateKey = "chat-authorization-guard";
        private readonly IChatAuthorization _chatAuthorization;
        private readonly AuthorizationSettings _settings;

        public ChatAuthorizationTemplateSourceScoped(IChatAuthorization chatAuthorization, AuthorizationSettings settings)
        {
            _chatAuthorization = chatAuthorization;
            _settings = settings;
        }

        public async Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            if (key != _templateKey)
            {
                throw new ArgumentException($"Unexpected key: {key}");
            }

            var isAuthorized = await _chatAuthorization.IsAuthorized();
            return isAuthorized ? _settings.AuthorizedTemplate : _settings.UnauthorizedTemplate;
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(key == _templateKey);
        }
    }
}
