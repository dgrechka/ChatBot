using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public class Chat
    {
        /// <summary>
        /// e.g. Telegram
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Unique identifier for the user within the namespace
        /// </summary>
        public string ID { get; set; }


        public Chat(string @namespace, string id)
        {
            Namespace = @namespace;
            ID = id;
        }

        public Chat(string str) {
            var parts = str.Split('|');
            if (parts.Length != 2) {
                throw new ArgumentException("Invalid chat string");
            }
            Namespace = parts[0];
            ID = parts[1];
        }

        public override bool Equals(object? obj)
        {
            if (obj is Chat chat)
            {
                return chat.Namespace == Namespace && chat.ID == ID;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Namespace, ID);
        }

        public override string ToString()
        {
            return $"{Namespace}|{ID}";
        }
    }
}
