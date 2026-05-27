// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// Manages active turns and handles webhook delivery to the customer's agent.
/// </summary>
public sealed class TurnManager
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SidecarOptions _options;
    private readonly ILogger<TurnManager> _logger;
    private readonly ConcurrentDictionary<string, TurnContext> _activeTurns = new();

    /// <summary>
    /// Initializes a new instance of <see cref="TurnManager"/>.
    /// </summary>
    public TurnManager(
        IHttpClientFactory httpClientFactory,
        IOptions<SidecarOptions> options,
        ILogger<TurnManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Registers an active turn and returns the turn ID.
    /// </summary>
    public string RegisterTurn(Builder.ITurnContext botTurnContext)
    {
        var turnId = Guid.NewGuid().ToString("N");
        var turnContext = new TurnContext(turnId, botTurnContext);
        _activeTurns[turnId] = turnContext;
        return turnId;
    }

    /// <summary>
    /// Gets the Bot Framework turn context for an active turn.
    /// </summary>
    public Builder.ITurnContext? GetBotTurnContext(string turnId)
    {
        return _activeTurns.TryGetValue(turnId, out var ctx) ? ctx.BotTurnContext : null;
    }

    /// <summary>
    /// Completes a turn and removes it from active tracking.
    /// </summary>
    public void CompleteTurn(string turnId)
    {
        _activeTurns.TryRemove(turnId, out _);
    }

    /// <summary>
    /// Delivers an activity to the customer's webhook and returns the response.
    /// </summary>
    public async Task<WebhookResult> DeliverToCustomerAsync(TurnPayload payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("CustomerWebhook");
        var webhookUrl = _options.Messaging.CustomerWebhook;

        _logger.LogDebug("Delivering turn {TurnId} ({Type}) to {Webhook}", payload.TurnId, payload.Type, webhookUrl);

        try
        {
            var response = await client.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Customer webhook returned {StatusCode} for turn {TurnId}", response.StatusCode, payload.TurnId);
                return WebhookResult.Error($"Webhook returned {response.StatusCode}");
            }

            // No content = customer will use outbound API
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return WebhookResult.NoReply();
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

            // SSE streaming response
            if (contentType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return WebhookResult.Streaming(stream);
            }

            // Non-streaming JSON response
            var turnResponse = await response.Content.ReadFromJsonAsync<TurnResponse>(cancellationToken: cancellationToken);
            return WebhookResult.Json(turnResponse);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Customer webhook timed out for turn {TurnId}", payload.TurnId);
            return WebhookResult.Error("Webhook request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to deliver turn {TurnId} to customer webhook", payload.TurnId);
            return WebhookResult.Error($"Webhook delivery failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates a Bot Framework Activity into the simplified TurnPayload.
    /// </summary>
    public TurnPayload TranslateActivity(string turnId, IActivity activity)
    {
        var payload = new TurnPayload
        {
            TurnId = turnId,
            Type = activity.Type ?? "message",
            ConversationId = activity.Conversation?.Id ?? string.Empty,
            ChannelId = activity.ChannelId!,
            Timestamp = activity.Timestamp?.ToString("O") ?? DateTimeOffset.UtcNow.ToString("O"),
            Text = activity.Text,
            ChannelData = activity.ChannelData,
        };

        if (activity.From != null)
        {
            string? aadObjectId = null;
            if (activity.From.Properties?.ContainsKey("aadObjectId") == true)
            {
                aadObjectId = activity.From.Properties["aadObjectId"].ToString();
            }

            payload.From = new AccountInfo
            {
                Id = activity.From.Id ?? string.Empty,
                Name = activity.From.Name,
                AadObjectId = aadObjectId,
            };
        }

        if (activity.Recipient != null)
        {
            payload.Recipient = new AccountInfo
            {
                Id = activity.Recipient.Id ?? string.Empty,
                Name = activity.Recipient.Name,
            };
        }

        // Attachments
        if (activity.Attachments?.Count > 0)
        {
            payload.Attachments = activity.Attachments.Select(a => new AttachmentInfo
            {
                ContentType = a.ContentType ?? string.Empty,
                ContentUrl = a.ContentUrl,
                Content = a.Content,
                Name = a.Name,
            }).ToList();
        }

        // ConversationUpdate
        if (activity.MembersAdded?.Count > 0)
        {
            payload.MembersAdded = activity.MembersAdded.Select(m => new AccountInfo
            {
                Id = m.Id ?? string.Empty,
                Name = m.Name,
            }).ToList();
        }

        if (activity.MembersRemoved?.Count > 0)
        {
            payload.MembersRemoved = activity.MembersRemoved.Select(m => new AccountInfo
            {
                Id = m.Id ?? string.Empty,
                Name = m.Name,
            }).ToList();
        }

        // Event
        if (string.Equals(activity.Type, "event", StringComparison.OrdinalIgnoreCase))
        {
            payload.EventName = activity.Name;
            payload.EventValue = activity.Value;
        }

        // Invoke
        if (string.Equals(activity.Type, "invoke", StringComparison.OrdinalIgnoreCase))
        {
            payload.InvokeName = activity.Name;
            payload.InvokeValue = activity.Value;
        }

        return payload;
    }

    private sealed class TurnContext
    {
        public string TurnId { get; }
        public Builder.ITurnContext BotTurnContext { get; }

        public TurnContext(string turnId, Builder.ITurnContext botTurnContext)
        {
            TurnId = turnId;
            BotTurnContext = botTurnContext;
        }
    }
}

/// <summary>
/// Result of delivering a turn to the customer's webhook.
/// </summary>
public sealed class WebhookResult
{
    /// <summary>
    /// Result type.
    /// </summary>
    public WebhookResultType ResultType { get; private init; }

    /// <summary>
    /// Non-streaming JSON response (when ResultType is JsonResponse).
    /// </summary>
    public TurnResponse? Response { get; private init; }

    /// <summary>
    /// SSE stream (when ResultType is StreamingResponse).
    /// </summary>
    public Stream? Stream { get; private init; }

    /// <summary>
    /// Error message (when ResultType is Failed).
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Creates a JSON response result.
    /// </summary>
    public static WebhookResult Json(TurnResponse? response) => new()
    {
        ResultType = WebhookResultType.JsonResponse,
        Response = response,
    };

    /// <summary>
    /// Creates a streaming response result.
    /// </summary>
    public static WebhookResult Streaming(Stream stream) => new()
    {
        ResultType = WebhookResultType.StreamingResponse,
        Stream = stream,
    };

    /// <summary>
    /// Creates a no-reply result (customer will use outbound API).
    /// </summary>
    public static WebhookResult NoReply() => new()
    {
        ResultType = WebhookResultType.NoReply,
    };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static WebhookResult Error(string message) => new()
    {
        ResultType = WebhookResultType.Failed,
        ErrorMessage = message,
    };
}

/// <summary>
/// Type of webhook result.
/// </summary>
public enum WebhookResultType
{
    /// <summary>
    /// Customer returned a JSON response.
    /// </summary>
    JsonResponse,

    /// <summary>
    /// Customer returned an SSE stream.
    /// </summary>
    StreamingResponse,

    /// <summary>
    /// Customer returned 204 No Content (will reply via outbound API).
    /// </summary>
    NoReply,

    /// <summary>
    /// Webhook delivery failed.
    /// </summary>
    Failed,
}
