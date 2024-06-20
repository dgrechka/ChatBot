using ChatBot.Interfaces;
using ChatBot.LLMs;
using ChatBot.Prompt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatBot.ScheduledTasks
{
    public class PersonalityTraitsExtractorScoped : IConversationProcessor
    {
        protected readonly ISummaryStorage _summaryStorage;
        private readonly IChatHistoryReader _chatHistoryReader;
        private readonly IPromptCompiler _promptCompiler;
        private readonly ITextGenerationLLMFactory _llmFactory;
        private readonly UserMessageContext _context;
        private readonly ConversationProcessingSettings _settings;
        private readonly ILogger<PersonalityTraitsExtractorScoped>? _logger;

        public PersonalityTraitsExtractorScoped(
            IPromptCompiler promptCompiler,
            ISummaryStorage summaryStorage,
            IChatHistoryReader chatHistoryReader,
            ConversationProcessingSettings settings,
            UserMessageContext context,
            ILogger<PersonalityTraitsExtractorScoped>? logger,
            ITextGenerationLLMFactory llmFactory)
        {
            _promptCompiler = promptCompiler;
            _llmFactory = llmFactory;
            _chatHistoryReader = chatHistoryReader;
            _summaryStorage = summaryStorage;
            _settings = settings;
            _context = context;
            _logger = logger;
        }

        public async Task Process(CancellationToken cancellationToken)
        {
            if (_context.Chat == null) {
                _logger?.LogWarning("Chat is not set in the context");
                return;
            }

            var latestSummary = await _summaryStorage.GetLatestSummary(_context.Chat, "UserProfileUpdate", cancellationToken);

            var propertyKeys = new List<string>(_settings.UserProfileProperties?.Keys ?? Enumerable.Empty<string>());

            var propertyDescriptions = new StringBuilder();
            foreach (var propKey in propertyKeys)
            {
                propertyDescriptions.AppendLine($"- {propKey}: {_settings.UserProfileProperties![propKey]}");
            }

            var propertyValues = await Task.WhenAll(propertyKeys.Select(key => _summaryStorage.GetLatestSummary(_context.Chat, "UserProfile" + key, cancellationToken)));

            Dictionary<string, string> userProfile = new();

            for (int i = 0; i < propertyKeys.Count; i++)
            {
                userProfile[propertyKeys[i]] = propertyValues[i]?.Content ?? string.Empty;
            }

            var runtimeTemplates = new Dictionary<string, string>
            {
                { "user-profile-fields-description", propertyDescriptions.ToString() },
            };

            // we fetch all messages that appear after the last summary
            var messages = _chatHistoryReader.GetMessagesSince(_context.Chat, latestSummary?.Time ?? DateTime.MinValue, cancellationToken);

            await foreach (var conversation in Helpers.ClusterConversations(messages, _settings.IdleConversationInterval))
            {
                var formattedConversation = Helpers.FormatConversation(
                    conversation,
                    out DateTime? firstMessageTime,
                    out DateTime? lastMessageTime,
                    out int counter);

                if (firstMessageTime == null || lastMessageTime == null)
                {
                    _logger?.LogWarning("No messages in conversation to process");
                    return;
                }

                runtimeTemplates["conversation-to-process"] = formattedConversation;

                var userProfileJson = JsonSerializer.Serialize(userProfile, options: new JsonSerializerOptions() { WriteIndented = true });
                runtimeTemplates["user-profile-json"] = userProfileJson;

                var accountingInfo = new AccountingInfo(_context.Chat, "ProfileUpdateInfoExtractor");
                
                var _llm = _llmFactory.CreateLLM(TextGenerationLLMRole.UserProfileUpdater);

                var callSettings = new LLMCallSettings()
                {
                    StopStrings = [.._llm.DefaultStopStrings, "\n```"],
                    ProduceJSON = true
                };

                var prompt = await _promptCompiler.CompilePrompt($"{_llm.PromptFormatIdentifier}-user-profile-updater", runtimeTemplates, cancellationToken);

                _logger?.LogInformation($"Generating user profile update for chat {_context.Chat}.");
                var summary = await _llm.GenerateResponseAsync(prompt, accountingInfo, callSettings, cancellationToken);

                try
                {
                    // decoding the JSON summary
                    var summaryJson = JsonSerializer.Deserialize<Dictionary<string, string>>(summary)!;
                    var updatedKeys = new List<string>();
                    foreach (var (key, value) in summaryJson)
                    {
                        if (!propertyKeys.Contains(key))
                        {
                            _logger?.LogWarning($"Generated user profile field {key} is not defined in the settings. Skipping");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(value) || value == userProfile[key])
                        {
                            _logger?.LogDebug($"Skipping update for {key} for {_context.Chat}. Empty or the same.");
                            continue;
                        }

                        _logger?.LogDebug($"Saving update for {key} for {_context.Chat}: {summaryJson[key]}");

                        await _summaryStorage.SaveSummary(_context.Chat, "UserProfile" + key, lastMessageTime.Value, value, cancellationToken);

                        userProfile[key] = value;
                        updatedKeys.Add(key);
                    }

                    await _summaryStorage.SaveSummary(_context.Chat, "UserProfileUpdate", lastMessageTime.Value, $"Updated {updatedKeys.Count} fields: {string.Join(", ", updatedKeys)}", cancellationToken);

                    _logger?.LogInformation($"Updated user profile ({summaryJson.Count} fields) for chat {_context.Chat}");
                    break;

                }
                catch (JsonException e)
                {
                    _logger?.LogWarning($"Error parsing JSON summary: {e.Message}");
                }
            }
        }
    }
}
