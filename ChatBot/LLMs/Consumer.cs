using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public class Consumer
    {
        /// <summary>
        /// e.g. Telegram
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Unique identifier for the user within the namespace
        /// </summary>
        public string ID { get; set; }
    }
}
