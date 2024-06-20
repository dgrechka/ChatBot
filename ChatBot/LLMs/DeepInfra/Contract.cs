using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot.LLMs.DeepInfra
{
    public enum StatusEnum { Unknown, Queued, Running, Succeeded, Failed }

    public class InferenceStatus
    {
        [JsonIgnore]
        public StatusEnum Status { get; set; } = StatusEnum.Unknown;

        [JsonPropertyName("status")]
        public string StatusString
        {
            get => Status.ToString().ToLower();
            set => Status = Enum.Parse<StatusEnum>(value, ignoreCase: true);
        }

        [JsonPropertyName("runtime_ms")]
        public int RuntimeMs { get; set; }

        [JsonPropertyName("cost")]
        public decimal Cost { get; set; }

        [JsonPropertyName("tokens_input")]
        public int TokensInput { get; set; }

        [JsonPropertyName("tokens_generated")]
        public int? TokensGenerated { get; set; }

    }
}
