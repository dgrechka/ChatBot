using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Processing.ChatTurn
{
    public interface IIdentityMapper
    {
        Chat TryGetUniformIdentifier(Chat chat);
    }

    public class PersonIdentifiers {
        public long? TelegramUserId { get; set; }
        public long? SignalMobileNum { get; set; }
    }

    public class IdentityMapper : IIdentityMapper
    {
        private readonly Dictionary<Chat, Chat> _map = new();

        public IdentityMapper(Dictionary<string, PersonIdentifiers> settings) {
            foreach (var (personName, identities) in settings) {
                if (identities.TelegramUserId != null) {
                    _map[new Chat("Telegram", identities.TelegramUserId.ToString()!)] = new Chat("Person", personName);
                }
                if (identities.SignalMobileNum != null) {
                    _map[new Chat("Signal", identities.SignalMobileNum.ToString()!)] = new Chat("Person", personName);
                }
            }
        }

        public Chat TryGetUniformIdentifier(Chat chat)
        {
            if(_map.TryGetValue(chat, out var uniformIdentifier)) {
                return uniformIdentifier;
            } else {
                return chat;
            }
        }
    }
}
