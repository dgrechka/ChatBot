using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class FileTemplateSource : ITemplateSource
    {
        public readonly string _templatesDirPath;

        public FileTemplateSource(string templatesDirPath)
        {
            _templatesDirPath = templatesDirPath;
        }

        public Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            return File.ReadAllTextAsync($"{Path.Combine(_templatesDirPath, key)}.md", cancellationToken);
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(File.Exists($"{Path.Combine(_templatesDirPath, key)}.md"));
        }
    }
}
