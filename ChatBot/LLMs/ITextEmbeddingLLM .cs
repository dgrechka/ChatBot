using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace ChatBot.LLMs
{
    public interface ITextEmbeddingLLM
    {
        Task<double[]> GenerateEmbeddingAsync(string text, AccountingInfo? accountingInfo, CancellationToken cancellationToken);

        public TextEmbeddingModels Model { get; }

        public int EmbeddingDimensionsCount { get; }
    }
}
