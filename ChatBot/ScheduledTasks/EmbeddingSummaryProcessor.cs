using ChatBot.Billing;
using ChatBot.LLMs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public class EmbeddingSummaryProcessor: ISummaryProcessor
    {
        private readonly ILogger<EmbeddingSummaryProcessor>? _logger;
        private readonly ITextEmbeddingLLMFactory _embeddingLLMFactory;
        private readonly IBillingLogger? _billingLogger;
        private readonly IEmbeddingStorage _embeddingStorage;
        private readonly ISummaryStorage _summaryStorage;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly string _summaryId = "Summary";
        private bool _isProcessingRequested = false;

        public EmbeddingSummaryProcessor(
            ITextEmbeddingLLMFactory embeddingLLMFactory,
            IEmbeddingStorage embeddingStorage,
            ISummaryStorage summaryStorage,
            IBillingLogger? billingLogger,
            ILogger<EmbeddingSummaryProcessor>? logger)
        {
            _embeddingLLMFactory = embeddingLLMFactory;
            _billingLogger = billingLogger;
            _embeddingStorage = embeddingStorage;
            _summaryStorage = summaryStorage;
            _logger = logger;
        }

        public Task NotifyNewSummaryPersisted(string summaryId)
        {
            _isProcessingRequested = true;
            _ = GenerateEmbeddingsIfNeeded();
            return Task.CompletedTask;
        }

        private async Task GenerateEmbeddingsIfNeeded() {
            var cancelationToken = new CancellationToken();
            await _semaphore.WaitAsync();
            try {
                if (!_isProcessingRequested)
                    return;
                _isProcessingRequested = false;

                _logger?.LogInformation("Embedding generator is waking up...");

                var latestProcessedSummaryId = await _embeddingStorage.GetLatestProcessedSummaryRecordId(_summaryId);

                int counter = 0;

                await foreach (var recordId in _summaryStorage.GetSummaryIdsSince(_summaryId, latestProcessedSummaryId, cancelationToken)) {
                    var summary = await _summaryStorage.GetSummaryByRecordId(recordId, cancelationToken);
                    if (summary == null) {
                        _logger?.LogWarning($"Summary with recordId {recordId} not found");
                        continue;
                    }

                    var _embeddingLLM = _embeddingLLMFactory.CreateLLM(TextEmbeddingLLMRole.ConvSummary);

                    var accountInfo = new AccountingInfo(summary.Chat, "GenEmbeddingForSummary");
                    var embeddings = (await _embeddingLLM.GenerateEmbeddingAsync(summary.Content, accountInfo, cancelationToken))
                        .Select(v => (float)v)
                        .ToArray();

                    await _embeddingStorage.SaveEmbedding(recordId, embeddings);
                    _logger?.LogInformation("Persisted embedding for summary {0} ({1}) of chat {2}", summary.RecordId, _summaryId, summary.Chat);
                    counter++;
                }

                _logger?.LogInformation("Embedding generator finished processing {0} summaries", counter);
            }
            finally {
                _semaphore.Release();
            }

            if (_isProcessingRequested)
                await GenerateEmbeddingsIfNeeded();
        }
    }

    public interface IEmbeddingStorage {
        Task<string?> GetLatestProcessedSummaryRecordId(string summaryId);
        Task SaveEmbedding(string summaryRecordId, float[] embedding);
    }
}
