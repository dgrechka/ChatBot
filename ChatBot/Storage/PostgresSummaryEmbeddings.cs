using ChatBot.LLMs;
using ChatBot.Prompt;
using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Storage
{
    public class PostgresSummaryEmbeddings : PostgresBasedStorage, IEmbeddingStorageWriter, IEmbeddingStorageLookup
    {
        private ILogger<PostgresSummaryEmbeddings>? _logger;
        private ITextEmbeddingLLM _embeddingLLM;
        private readonly string _tableName;
        private const double similarityThreshold = -0.5;
        private const int maxSummariesForRAG = 10;

        public PostgresSummaryEmbeddings(
            PostgresConnection connection,
            ITextEmbeddingLLMFactory embeddingLLMFactory,
            ILogger<PostgresSummaryEmbeddings>? logger) : base(connection)
        {
            _logger = logger;
            _embeddingLLM = embeddingLLMFactory.CreateLLM(TextEmbeddingLLMRole.ConvSummary);
            _tableName = $"SummaryEmbeddings_{_embeddingLLM.Model.ToString()}";
        }

        protected override async Task InitializeCore(CancellationToken token)
        {
            using var conn = _postgres.DataSource.CreateConnection();
            try
            {
                await conn.OpenAsync();

                // creating extension requires superuser role.

                //await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", conn))
                //{
                //    await cmd.ExecuteNonQueryAsync();
                //}
                using var cmd = conn.CreateCommand();

                cmd.CommandText = $@"
                    CREATE TABLE IF NOT EXISTS {_tableName} (
                        EmbeddingId serial NOT NULL PRIMARY KEY,
                        SummaryRecordId integer NOT NULL references Summaries(RecordId) UNIQUE,
                        Embedding vector({_embeddingLLM.EmbeddingDimensionsCount})
                    )";

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await conn.CloseAsync();
            }

        }

        public async Task<string?> GetLatestProcessedSummaryRecordId(string summaryId)
        {
            var cancelToken = CancellationToken.None;

            await EnsureInitialized(cancelToken);

            using var conn = _postgres.DataSource.CreateConnection();
            try
            {
                await conn.OpenAsync();

                // inner join on the summary table, select latest summary record id which we have embedding for
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    SELECT MAX(s.RecordId) FROM {_tableName} as e
                    INNER JOIN Summaries as s on s.RecordId = e.SummaryRecordId
                    WHERE s.SummaryId = @SummaryId";

                cmd.Parameters.AddWithValue("SummaryId", summaryId);

                var result = (await cmd.ExecuteScalarAsync(cancelToken));
                return (result as int?)?.ToString();
                
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        public async Task SaveEmbedding(string summaryRecordId, float[] embedding)
        {
            var cancelToken = CancellationToken.None;

            await EnsureInitialized(cancelToken);

            using var conn = _postgres.DataSource.CreateConnection();
            try
            {
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                cmd.CommandText = $@"
                    INSERT INTO {_tableName} (SummaryRecordId, Embedding)
                    VALUES (@SummaryRecordId, @Embedding)";
                
                cmd.Parameters.AddWithValue("SummaryRecordId", int.Parse(summaryRecordId));

                var vector = new Pgvector.Vector(embedding);
                cmd.Parameters.AddWithValue("Embedding", vector);

                await cmd.ExecuteNonQueryAsync(cancelToken);
                
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        public async IAsyncEnumerable<Summary> GetRelevantSummaries(string summaryId, Chat chat, float[] queryEmbedding, [EnumeratorCancellation]CancellationToken cancellationToken) {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    SELECT s.RecordId, s.Timestamp, s.Summary, e.Embedding <#> @QueryEmbedding AS distance
                    FROM {_tableName} as e
                    INNER JOIN Summaries as s on s.RecordId = e.SummaryRecordId
                    WHERE s.SummaryId = @SummaryId AND s.ChatId = @ChatId AND e.Embedding <#> @QueryEmbedding < @SimilarityThreshold
                    ORDER BY e.Embedding <#> @QueryEmbedding
                    LIMIT @MaxSummaries";
                command.Parameters.AddWithValue("QueryEmbedding", new Pgvector.Vector(queryEmbedding));
                command.Parameters.AddWithValue("SummaryId", summaryId);
                command.Parameters.AddWithValue("ChatId", chat.ToString());
                command.Parameters.AddWithValue("SimilarityThreshold", similarityThreshold);
                command.Parameters.AddWithValue("MaxSummaries", maxSummariesForRAG);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var summary = new Summary(
                        reader.GetInt32(0).ToString(),
                        reader.GetDateTime(1),
                        reader.GetString(2),
                        chat);
                    yield return summary;
                    _logger?.LogDebug($"Dist: {reader.GetFloat(3)}\n{summary.Content}");
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
