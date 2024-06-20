using ChatBot.LLMs;
using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Storage
{
    public class PostgresSummaryStorage : PostgresBasedStorage, ISummaryStorage
    {
        private readonly ILogger<PostgresSummaryStorage> logger;

        public PostgresSummaryStorage(ILogger<PostgresSummaryStorage> logger, PostgresConnection connection) : base(connection)
        {
            this.logger = logger;
        }

        protected override async Task InitializeCore(CancellationToken token)
        {
            logger.LogInformation("Initializing PostgresSummaryStorage");
            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(token);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Summaries (
                                RecordId bigserial NOT NULL PRIMARY KEY,
                                ChatId TEXT NOT NULL,
                                SummaryId TEXT NOT NULL,
                                Timestamp TIMESTAMPTZ NOT NULL,
                                Summary TEXT NOT NULL
                            );
                        ";
            await command.ExecuteNonQueryAsync(token);

            // add index on ChatId, SummaryId, Timestamp
            command.CommandText = @"
                            CREATE INDEX IF NOT EXISTS ChatId_SummaryId_Timestamp_Index
                            ON Summaries (ChatId, SummaryId, Timestamp)";
            await command.ExecuteNonQueryAsync(token);
            await connection.CloseAsync();
        }

        public async Task<Summary?> GetLatestSummary(Chat chat, string summaryId, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT RecordId, Timestamp, Summary
                    FROM Summaries
                    WHERE ChatId = @ChatId AND SummaryId = @SummaryId
                    ORDER BY Timestamp DESC
                    LIMIT 1";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@ChatId", chat.ToString()));
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryId", summaryId));

                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                return await reader.ReadAsync(cancellationToken) ?
                    new Summary(reader.GetInt32(0).ToString(), reader.GetDateTime(1), reader.GetString(2), chat)
                    : null;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async Task<Summary?> GetSummaryByRecordId(string recordId, CancellationToken cancellationToken) {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT ChatId, Timestamp, Summary
                    FROM Summaries
                    WHERE RecordId = @RecordId
                ";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@RecordId", int.Parse(recordId)));

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await reader.ReadAsync(cancellationToken) ?
                    new Summary(recordId, reader.GetDateTime(1), reader.GetString(2), new Chat(reader.GetString(0)))
                    : null;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async IAsyncEnumerable<string> GetSummaryIdsSince(
            string summaryId,
            string? summaryRecordId,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            await EnsureInitialized(cancellationToken);
using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();

                string recordIdFiltering = string.Empty;
                if (summaryRecordId != null)
                {
                    int recordIdSince = int.Parse(summaryRecordId);
                    recordIdFiltering = " AND Id > @SummaryRecordId";
                    command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryRecordId", recordIdSince));
                }


                command.CommandText = $@"
                    SELECT Id
                    FROM Summaries
                    WHERE SummaryId = @SummaryId {recordIdFiltering}
                ";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryId", summaryId));
                

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    yield return reader.GetString(0);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async IAsyncEnumerable<Chat> GetChatsWithSummaries(string summaryId, [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT DISTINCT ChatId
                    FROM Summaries
                    WHERE SummaryId = @SummaryId
                ";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryId", summaryId));

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var split = reader.GetString(0).Split('|');
                    yield return new Chat(split[0], split[1]);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async Task SaveSummary(Chat chat, string summaryId, DateTime time, string summary, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Summaries (ChatId, SummaryId, Timestamp, Summary)
                    VALUES (@ChatId, @SummaryId, @Timestamp, @Summary)
                ";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@ChatId", chat.ToString()));
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryId", summaryId));
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@Timestamp", time));
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@Summary", summary));

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to save summary");
                throw;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
