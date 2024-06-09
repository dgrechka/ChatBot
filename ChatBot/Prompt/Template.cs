using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public class Placeholder
    {
        public string Name { get; }
        public int StartIdx { get; }
        public int EndIdx { get; }

        public Placeholder(string name, int startIdx, int endIdx)
        {
            Name = name;
            this.StartIdx = startIdx;
            this.EndIdx = endIdx;
        }
    }

    public class Template
    {
        private readonly List<Placeholder> placeholders = new List<Placeholder>();
        private readonly string originalTemplate;

        private static readonly char placeholderStartMarker = '◄';
        private static readonly char placeholderEndMarker = '►';

        public Template(string rawString) {
            originalTemplate = rawString;
            int searchIdx = 0;
            while (searchIdx < rawString.Length) {
                int startIdx = rawString.IndexOf(placeholderStartMarker, searchIdx);
                // TODO: Handle escaped markers
                if (startIdx == -1) {
                    break;
                }
                int endIdx = rawString.IndexOf(placeholderEndMarker, startIdx);
                if (endIdx == -1) {
                    throw new ArgumentException("Unmatched placeholder start marker");
                }
                string placeholderName = rawString.Substring(startIdx + 1, endIdx - startIdx - 1);
                placeholders.Add(new Placeholder(placeholderName.ToLowerInvariant(), startIdx, endIdx+1));
                searchIdx = endIdx + 1;
            }
        }

        public IReadOnlyList<string> Placeholders => placeholders.Select(p => p.Name).Distinct().ToList();

        public string Render(Dictionary<string, string> placeholderValues) {
            List<string> notDefinedPlaceholders = placeholders
                .Select(p => p.Name)
                .Where(p => !placeholderValues.ContainsKey(p.ToLowerInvariant()))
                .ToList();

            if(notDefinedPlaceholders.Count > 0) {
                throw new ArgumentException($"The following placeholders are not defined: {string.Join(", ", notDefinedPlaceholders)}");
            }

            StringBuilder sb = new StringBuilder();
            int lastIdx = 0;
            foreach (var placeholder in placeholders) {
                sb.Append(originalTemplate.Substring(lastIdx, placeholder.StartIdx - lastIdx));
                sb.Append(placeholderValues[placeholder.Name]);
                lastIdx = placeholder.EndIdx;
            }
            sb.Append(originalTemplate.Substring(lastIdx));
            return sb.ToString();
        }
    }
}
