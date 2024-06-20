using ChatBot.LLMs;
using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Storage
{
    public class PostgresSummaryEmbeddings : PostgresBasedStorage, IEmbeddingStorage
    {
        private ILogger<PostgresSummaryEmbeddings>? _logger;
        private ITextEmbeddingLLM _embeddingLLM;
        private readonly string _tableName;

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
                        SummaryRecordId integer NOT NULL references Summaries(RecordId),
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
                    INNER JOIN Summaries as s on s.RecordId = e.SummaryRecordId";
                
                var result = (await cmd.ExecuteScalarAsync(cancelToken)) as int?;
                return result?.ToString();
                
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
    }
}
