using ChatBot.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ChatBot.Auxiliary
{
    public class SignalSettings {
        public string ApiGatewayAddress { get; set; } = string.Empty;
        public string SelfNumber { get; set; } = string.Empty;

    }

    public class SignalRestClient : ISignalClient, IDisposable
    {
        private readonly SignalSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly ClientWebSocket _clientWebSocket;
        private readonly ILogger<SignalRestClient>? _logger;

        public SignalRestClient(SignalSettings settings, ILogger<SignalRestClient>? logger)
        {
            _settings = settings;
            _httpClient = new HttpClient() { BaseAddress= new Uri(settings.ApiGatewayAddress)};
            _clientWebSocket = new ClientWebSocket();
            _logger = logger;
        }

        public Task ConfirmMessageProcessed(string recipient, ulong timestamp)
        {
            return Task.CompletedTask;
        }

        public async Task ConfirmMessageRead(string recipient, ulong timestamp)
        {
            var payload = new MessageReadConfirmationPayload(recipient, timestamp);
            var jsonConetent = JsonContent.Create(payload);
            var res = await _httpClient.PostAsync($"v1/receipts/{_settings.SelfNumber}", jsonConetent);
            var content = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
        }

        public async IAsyncEnumerable<IncomingSignalMessage> GetMessages([EnumeratorCancellation]CancellationToken cancellationToken)
        {
            await _clientWebSocket.ConnectAsync(new Uri(_settings.ApiGatewayAddress.Replace("http","ws")+ "/v1/receive/"+_settings.SelfNumber), cancellationToken);
            _logger?.LogInformation("Connected to Signal API Gateway");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var buffer = new ArraySegment<byte>(new byte[4096]);
                    var res = await _clientWebSocket.ReceiveAsync(buffer, cancellationToken);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    if (buffer.Array == null || res.Count == 0)
                    {
                        continue;
                    }

                    var str = Encoding.UTF8.GetString(buffer.Array[..res.Count]);
                    var message = JsonSerializer.Deserialize<IncomingMessagePayload>(str);
                    if (message != null && message?.Envelope?.DataMessage?.Message != null)
                    {
                        yield return new IncomingSignalMessage(
                            message.Envelope.SourceNumber ?? string.Empty,
                            message.Envelope.DataMessage.Timestamp ?? 0L,
                            message.Envelope.DataMessage.Message);
                    }
                }
            }
            finally
            {
                if(_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.CloseReceived || _clientWebSocket.State == WebSocketState.CloseSent)
                {
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                }
            }
        }

        public async Task SendTypingIndicator(string recipient, CancellationToken cancellationToken)
        {
            var payload = new TypingIndicatorPayload(recipient);
            var jsonConetent = JsonContent.Create(payload);
            var res = await _httpClient.PutAsync($"v1/typing-indicator/{_settings.SelfNumber}", jsonConetent);
            var content = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
        }

        public async Task SendMessageAsync(OutgoingSignalMessage message, CancellationToken cancellationToken)
        {
            if(cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var payload = new SendMessagePayload([message.Recipient], _settings.SelfNumber, message.Message);
            var jsonConetent = JsonContent.Create(payload);
            var res = await _httpClient.PostAsync($"v2/send", jsonConetent);
            var content = await res.Content.ReadAsStringAsync();
            res.EnsureSuccessStatusCode();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _clientWebSocket.Dispose();
        }
    }

    public class MessageReadConfirmationPayload {
        [JsonPropertyName("recipient")]
        public string Recipient { get; }
        [JsonPropertyName("timestamp")]
        public ulong Timestamp { get; }

        [JsonPropertyName("receipt_type")]
        public string ReceiptType => "read";

        public MessageReadConfirmationPayload(string recipient, ulong timestamp)
        {
            Recipient = recipient;
            Timestamp = timestamp;
        }
    }

    public class SendMessagePayload {
        [JsonPropertyName("recipients")]
        public string[] Recipients { get; }

        [JsonPropertyName("number")]
        public string Number { get; }

        [JsonPropertyName("message")]
        public string Message { get; }

        public SendMessagePayload(string[] recipients, string number, string message)
        {
            Recipients = recipients;
            Number = number;
            Message = message;
        }
    }

    public class TypingIndicatorPayload {
        [JsonPropertyName("recipient")]
        public string Recipient { get; }

        public TypingIndicatorPayload(string recipient)
        {
            Recipient = recipient;
        }
    }

    public class DataMessage {
        [JsonPropertyName("timestamp")]
        public ulong? Timestamp { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class Envelope {
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("sourceNumber")]
        public string? SourceNumber { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("dataMessage")]
        public DataMessage? DataMessage { get; set; }
    }

    public class IncomingMessagePayload {
        [JsonPropertyName("envelope")]
        public Envelope? Envelope { get; set; }
    }
}
