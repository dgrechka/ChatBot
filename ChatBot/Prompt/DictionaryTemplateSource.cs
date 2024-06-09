using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class DictionaryTemplateSource: ITemplateSource
    {
        private readonly Dictionary<string, string> _templates;

        public DictionaryTemplateSource(Dictionary<string, string> templates)
        {
            _templates = templates;
        }

        public Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_templates[key]);
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(_templates.ContainsKey(key));
        }
    }
}
