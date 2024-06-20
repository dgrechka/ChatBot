using ChatBot.LLMs;
using ChatBot.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Billing
{
    public class PostgresBillingLogger : PostgresBasedStorage, IBillingLogger
    {
        private readonly ILogger<PostgresBillingLogger>? _logger;

        public PostgresBillingLogger(ILogger<PostgresBillingLogger>? logger, PostgresConnection postgres) : base(postgres)
        {
            _logger = logger;
        }

        protected override async Task InitializeCore(CancellationToken token)
        {
            _logger?.LogInformation("Initializing PostgresBillingLogger");
            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(token);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS LLMCost (
                                Id SERIAL PRIMARY KEY,
                                ChatId TEXT NOT NULL,
                                Provider TEXT NOT NULL,
                                Model TEXT NOT NULL,
                                CallPurpose TEXT NOT NULL,
                                InputTokenCount INT NOT NULL,
                                GeneratedTokenCount INT NOT NULL,
                                EstimatedCost DECIMAL NOT NULL,
                                Currency TEXT NOT NULL,
                                Timestamp TIMESTAMPTZ NOT NULL
                            );
                        ";
            await command.ExecuteNonQueryAsync(token);
            await connection.CloseAsync();
        }

        public async Task LogLLMCost(AccountingInfo accountingInfo, string provider, string model, int inputTokenCount, int generatedTokenCount, decimal estimatedCost, string currency, CancellationToken token)
        {
            await EnsureInitialized(token);

            using var connection = _postgres.DataSource.CreateConnection();
            await connection.OpenAsync(token);
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO LLMCost (ChatId, Provider, Model, CallPurpose, InputTokenCount, GeneratedTokenCount, EstimatedCost, Currency, Timestamp)
                VALUES (@ChatId, @Provider, @Model, @CallPurpose, @InputTokenCount, @GeneratedTokenCount, @EstimatedCost, @Currency, @Timestamp);";
            command.Parameters.AddWithValue("ChatId", accountingInfo.Chat.ToString());
            command.Parameters.AddWithValue("Provider", provider);
            command.Parameters.AddWithValue("Model", model);
            command.Parameters.AddWithValue("CallPurpose", accountingInfo.CallPurpose);
            command.Parameters.AddWithValue("InputTokenCount", inputTokenCount);
            command.Parameters.AddWithValue("GeneratedTokenCount", generatedTokenCount);
            command.Parameters.AddWithValue("EstimatedCost", estimatedCost);
            command.Parameters.AddWithValue("Currency", currency);
            command.Parameters.AddWithValue("Timestamp", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(token);
            await connection.CloseAsync();
        }
    }
}
