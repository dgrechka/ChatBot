﻿using ChatBot.Interfaces;
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
        private readonly ITextGenerationLLM _llm;
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
            ITextGenerationLLM llm)
        {
            _promptCompiler = promptCompiler;
            _llm = llm;
            _chatHistoryReader = chatHistoryReader;
            _summaryStorage = summaryStorage;
            _settings = settings;
            _context = context;
            _logger = logger;
        }

        public async Task Process(CancellationToken cancellationToken)
        {
            var latestSummary = await _summaryStorage.GetLatestSummary(_context.Chat, "UserProfileUpdate", cancellationToken);

            var propertyKeys = new List<string>(_settings.UserProfileProperties?.Keys ?? Enumerable.Empty<string>());

            var propertyDescriptions = new StringBuilder();
            foreach (var propKey in propertyKeys)
            {
                propertyDescriptions.AppendLine($"- {propKey}: {_settings.UserProfileProperties[propKey]}");
            }

            var propertyValues = await Task.WhenAll(propertyKeys.Select(key => _summaryStorage.GetLatestSummary(_context.Chat, "UserProfile" + key, cancellationToken)));

            Dictionary<string,string> userProfile = new();

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
                runtimeTemplates["conversation-to-process"] = formattedConversation;

                var userProfileJson = JsonSerializer.Serialize(userProfile, options: new JsonSerializerOptions() { WriteIndented = true });
                runtimeTemplates["user-profile-json"] = userProfileJson;
                
                var accountingInfo = new AccountingInfo(_context.Chat, "ProfileUpdateInfoExtractor");
                var callSettings = new CallSettings()
                {
                    StopStrings = ["<|eot_id|>", "\n```"],
                };

                var prompt = await _promptCompiler.CompilePrompt("llama3-user-profile-updater", runtimeTemplates, cancellationToken);

                for (int i = 0; i < 5; i++)
                {
                    // that to to workaround the cases when the model generates something in addition to JSON
                    callSettings.Temperature = i switch
                    {
                        0 => 0.7, // default llama3 val
                        1 => 0.5,
                        2 => 0.3,
                        3 => 0.8,
                        4 => 1.0,
                    };

                    _logger?.LogInformation($"Attempt #{i+1} to generate user profile update for chat {_context.Chat}. Temperature {callSettings.Temperature}");
                    var summary = await _llm.GenerateResponseAsync(prompt, accountingInfo, callSettings, cancellationToken);

                 
                    try
                    {
                        // decoding the JSON summary
                        var summaryJson = JsonSerializer.Deserialize<Dictionary<string, string>>(summary);
                        foreach (var (key,value) in summaryJson)
                        {
                            if(!propertyKeys.Contains(key))
                            {
                                _logger?.LogWarning($"Generated user profile field {key} is not defined in the settings. Skipping");
                                continue;
                            }
                            if(string.IsNullOrWhiteSpace(value) || value == userProfile[key])
                            {
                                _logger?.LogDebug($"Skipping update for {key} for {_context.Chat}. Empty or the same.");
                                continue;
                            }

                            _logger?.LogDebug($"Saving update for {key} for {_context.Chat}: {summaryJson[key]}");

                            await _summaryStorage.SaveSummary(_context.Chat, "UserProfile" + key, lastMessageTime.Value, value, cancellationToken);

                            userProfile[key] = value;
                        }

                        await _summaryStorage.SaveSummary(_context.Chat, "UserProfileUpdate", lastMessageTime.Value, $"Updated {summaryJson.Count} fields: {string.Join(", ", summaryJson.Keys)}", cancellationToken);

                        _logger?.LogInformation($"Updated user profile ({summaryJson.Count} fields) for chat {_context.Chat}");
                        break;
                        
                    }
                    catch (JsonException e)
                    {
                        _logger?.LogError($"Error parsing JSON summary: {e.Message}");
                    }
                }
            }
        }
    }
}