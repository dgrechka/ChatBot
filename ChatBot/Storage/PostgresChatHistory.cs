using ChatBot.Interfaces;
using ChatBot.LLMs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using ChatBot.Storage;
using System.Runtime.CompilerServices;

namespace ChatBot.Chats
{
    public class PostgresChatHistory : PostgresBasedStorage, IChatHistoryWriter, IChatHistoryReader
    {
        private readonly ILogger<PostgresChatHistory> _logger;
        
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
        }

        public async IAsyncEnumerable<(Chat, DateTime)> GetChatsLastMessageTime([EnumeratorCancellation] CancellationToken cancellationToken) {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT ChatId, MAX(Timestamp)
                    FROM MessageHistory
                    GROUP BY ChatId;
                ";
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return (new Chat(reader.GetString(0)), reader.GetDateTime(1));
            }
            await connection.CloseAsync();

        }

        public async IAsyncEnumerable<Message> GetMessagesSince(Chat chat, DateTime time, [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT Timestamp, Sender, Content
                    FROM MessageHistory
                    WHERE ChatId = @ChatId AND Timestamp > @Time
                    ORDER BY Timestamp ASC;
                ";
            command.Parameters.AddWithValue("ChatId", chat.ToString());
            command.Parameters.AddWithValue("Time", time);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                Message? message = null;
                try
                {
                    message = new Message(reader.GetDateTime(0), Enum.Parse<Author>(reader.GetString(1)), reader.GetString(2));
                }
                catch (ArgumentException e)
                {
                    _logger.LogWarning("Failed to parse message author: {0}", e.Message);
                }

                if(message != null)
                {
                    yield return message;
                }
            }
            await connection.CloseAsync();
        }

        public async IAsyncEnumerable<Chat> GetChats([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await EnsureInitialized(cancellationToken);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                    SELECT DISTINCT ChatId
                    FROM MessageHistory;
                ";
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new Chat(reader.GetString(0));
            }
            await connection.CloseAsync();
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
