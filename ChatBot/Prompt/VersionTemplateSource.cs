using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class VersionTemplateSource : ITemplateSource
    {
        private static readonly string[] keys = [ "app-version" , "app-build-time" ];

        public Task<string> GetValue(string key, CancellationToken cancellationToken)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var buildDate = new FileInfo(assembly.Location).LastWriteTime;
            return key switch
            {
                "app-version" => Task.FromResult(assembly.GetName().Version?.ToString() ?? "0.0.0"),
                "app-build-time" => Task.FromResult(buildDate.ToString("yyyy-MM-dd HH:mm:ss")),
                _ => Task.FromResult(string.Empty),
            };
        }

        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(keys.Contains(key));
        }
    }
}
