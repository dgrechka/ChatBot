using ChatBot.Interfaces;
using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Logging;
using ChatBot.Persistence;
using Newtonsoft.Json.Linq;
using System.Data.Common;

namespace ChatBot.Chats
{
    public class PostgresChatHistory : PostgresBasedStorage, IChatHistory
    {
        private readonly PostgresConnection _postgres;
        private readonly ILogger<PostgresChatHistory> _logger;
        private bool _initialized = false;
        private SemaphoreSlim _initSem = new SemaphoreSlim(1);

        protected override async Task InitializeCore(CancellationToken token)
        {
            _logger.LogInformation("Initializing PostgresChatHistory");
            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(token);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS MessageHistory (
                                Id SERIAL PRIMARY KEY,
                                ChatId TEXT NOT NULL,
                                Timestamp TIMESTAMPTZ NOT NULL,
                                Sender TEXT NOT NULL,
                                Content TEXT NOT NULL
                            );
                        ";
            await command.ExecuteNonQueryAsync(token);

            // add index on ChatId, Timestamp
            command.CommandText = @"
                            CREATE INDEX IF NOT EXISTS ChatId_Timestamp_Index
                            ON MessageHistory (ChatId, Timestamp)";
            await command.ExecuteNonQueryAsync(token);
            await connection.CloseAsync();
        }

        public PostgresChatHistory(ILogger<PostgresChatHistory> logger, PostgresConnection postgres)
            : base(postgres)
        {
            _logger = logger;
            _postgres = postgres;
        }

        public async Task<IEnumerable<Message>> GetMessages(Chat chat, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT Timestamp, Sender, Content
                    FROM MessageHistory
                    WHERE ChatId = @ChatId
                    ORDER BY Timestamp DESC
                    LIMIT 10;
                ";
            command.Parameters.AddWithValue("ChatId", chat.ToString());
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var messages = new List<Message>();
            while (await reader.ReadAsync(cancellationToken))
            {
                try
                {
                    var message = new Message
                    {
                        Timestamp = reader.GetDateTime(0),
                        Author = Enum.Parse<Author>(reader.GetString(1)),
                        Content = reader.GetString(2)
                    };
                    messages.Add(message);
                }
                catch (ArgumentException e)
                {
                    _logger.LogWarning("Failed to parse message author: {0}", e.Message);
                }
            }
            await connection.CloseAsync();

            // sorting messages in ascending order
            messages.Reverse();

            return messages;
        }

        public async Task LogMessages(Chat chat, IEnumerable<Message> messages, CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            foreach (var message in messages)
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                        INSERT INTO MessageHistory (ChatId, Timestamp, Sender, Content)
                        VALUES (@ChatId, @Timestamp, @Sender, @Content);
                    ";
                command.Parameters.AddWithValue("ChatId", chat.ToString());
                command.Parameters.AddWithValue("Timestamp", message.Timestamp);
                command.Parameters.AddWithValue("Sender", message.Author.ToString());
                command.Parameters.AddWithValue("Content", message.Content);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            await connection.CloseAsync();
        }
    }
}
