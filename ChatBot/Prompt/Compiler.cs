using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public interface ITemplateSource
    {
        Task<bool> HasKey(string key);
        Task<string> GetValue(string key, CancellationToken cancellationToken);
    }
    public interface IPromptCompiler
    {
        Task<string> CompilePrompt(string keyToCompile, Dictionary<string,string>? additionalContext, CancellationToken cancellationToken);
    }

    public class Compiler: IPromptCompiler
    {
        private readonly IEnumerable<ITemplateSource> _templateSources;
        private readonly ILogger<Compiler>? _logger;

        public Compiler(ILogger<Compiler>? logger, IEnumerable<ITemplateSource> templateSources)
        {
            _templateSources = templateSources;
            _logger = logger;
        }

        public async Task<string> CompilePrompt(string keyToCompile, Dictionary<string, string>? additionalContext, CancellationToken cancellationToken)
        {
            var sw = new System.Diagnostics.Stopwatch();

            var effectiveTemplateSources = _templateSources;
            if(additionalContext != null)
            {
                var runtimeTemplateSource = new DictionaryTemplateSource(additionalContext);
                effectiveTemplateSources = _templateSources.Append(runtimeTemplateSource);
            }

            // 1. forward pass. gather dependencies starting with keyToCompile, and build the dependencies graph
            Dictionary<string, HashSet<string>> dependencies = new();
            Dictionary<string, HashSet<string>> dependents = new();
            Dictionary<string,Template> templates = new();
            HashSet<string> zeroDependenciesKeys = new();
            Queue<string> queue = new();
            queue.Enqueue(keyToCompile);
            while(queue.Count > 0)
            {
                var key = queue.Dequeue();
                if (dependencies.ContainsKey(key))
                {
                    continue;
                }
                _logger?.LogDebug($"Processing {key}");

                foreach (var source in effectiveTemplateSources)
                {
                    if(await source.HasKey(key))
                    {
                        var rawTemplateStr = await source.GetValue(key, cancellationToken);
                        var template = new Template(rawTemplateStr);
                        templates[key] = template;

                        _logger?.LogDebug($"Loaded template {key}. References {template.Placeholders.Count} placeholders");

                        dependencies[key] = new HashSet<string>(template.Placeholders);

                        foreach(var dependency in template.Placeholders)
                        {
                            queue.Enqueue(dependency);

                            if(!dependents.TryGetValue(dependency, out HashSet<string>? value))
                            {
                                value = new HashSet<string>();
                                dependents[dependency] = value;
                            }

                            value.Add(key);

                            if(zeroDependenciesKeys.Contains(dependency))
                            {
                                zeroDependenciesKeys.Remove(dependency);
                            }
                        }

                        if(template.Placeholders.Count == 0)
                        {
                            zeroDependenciesKeys.Add(key);
                        }
                    }
                }
            }

            _logger?.LogDebug($"Dependencies graph has {dependencies.Count} nodes");

            // 2. building compilation order
            if(zeroDependenciesKeys.Count == 0)
            {
                throw new InvalidOperationException($"No zero dependencies keys found for {keyToCompile}. Check for circular dependency");
            }
            Dictionary<string,string> compiledTemplates = new();
            foreach(var key in zeroDependenciesKeys)
            {
                queue.Enqueue(key);
            }
            while(queue.Count > 0)
            {
                var key = queue.Dequeue();
                _logger?.LogDebug($"Compiling {key}");

                var template = templates[key];
                var compiledTemplate = template.Render(compiledTemplates);
                compiledTemplates[key] = compiledTemplate;
                _logger?.LogDebug($"Compiled {key}. Result template length is {compiledTemplate.Length}");

                if(dependents.TryGetValue(key, out HashSet<string>? value))
                {
                    foreach(var dependent in value)
                    {
                        dependencies[dependent].Remove(key);
                        if(dependencies[dependent].Count == 0)
                        {
                            queue.Enqueue(dependent);
                        }
                    }
                }
            }

            sw.Stop();
            
            var compiledTemplateStr = compiledTemplates[keyToCompile];

            _logger?.LogInformation($"{compiledTemplates.Count} templates compiled. Compilation took {sw.Elapsed}. {keyToCompile} length is {compiledTemplateStr.Length}");

            return compiledTemplateStr;
        }
    }
}
