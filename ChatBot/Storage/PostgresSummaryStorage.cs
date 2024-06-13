using ChatBot.LLMs;
using ChatBot.ScheduledTasks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<DateTime?> GetLatestSummary(Chat chat, string summaryId, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT MAX(Timestamp)
                    FROM Summaries
                    WHERE ChatId = @ChatId AND SummaryId = @SummaryId
                ";
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@ChatId", chat.ToString()));
                command.Parameters.Add(new Npgsql.NpgsqlParameter("@SummaryId", summaryId));

                var result = await command.ExecuteScalarAsync(cancellationToken);

                return result is DBNull ? null : (DateTime?)result;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        public async IAsyncEnumerable<Chat> GetChatsWithSummaries(string summaryId, CancellationToken cancellationToken)
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
