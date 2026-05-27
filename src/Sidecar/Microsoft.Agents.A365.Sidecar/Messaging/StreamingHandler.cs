// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// Reads SSE events from the customer's streaming response and translates them
/// into Activity Protocol streaming activities sent back to the channel.
/// </summary>
public sealed class StreamingHandler
{
    private readonly ILogger<StreamingHandler> _logger;
    private readonly int _timeoutSeconds;

    /// <summary>
    /// Initializes a new instance of <see cref="StreamingHandler"/>.
    /// </summary>
    public StreamingHandler(IOptions<SidecarOptions> options, ILogger<StreamingHandler> logger)
    {
        _logger = logger;
        _timeoutSeconds = options.Value.Messaging.Streaming.TimeoutSeconds;
    }

    /// <summary>
    /// Processes an SSE stream from the customer and sends activities to the channel.
    /// </summary>
    /// <param name="sseStream">The SSE response stream from the customer's webhook.</param>
    /// <param name="turnContext">The Bot Framework turn context for sending replies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final complete text, or null if an error occurred.</returns>
    public async Task<string?> ProcessStreamAsync(
        Stream sseStream,
        Builder.ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(sseStream, Encoding.UTF8);
        var timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        string? finalText = null;
        var accumulatedText = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            string? line;
            try
            {
                line = await reader.ReadLineAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("SSE stream silence timeout ({Timeout}s) exceeded", _timeoutSeconds);
                await SendErrorToChannel(turnContext, "Stream timeout - agent did not respond in time", cancellationToken);
                return null;
            }

            // End of stream
            if (line == null)
            {
                break;
            }

            // SSE protocol: empty lines are event delimiters, skip them
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // Parse SSE "data: {...}" lines
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line.Substring(6);
            StreamingEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<StreamingEvent>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE event: {Line}", line);
                continue;
            }

            if (evt == null)
            {
                continue;
            }

            switch (evt.Type)
            {
                case "typing":
                    await turnContext.SendActivityAsync(
                        new Activity { Type = ActivityTypes.Typing },
                        cancellationToken);
                    break;

                case "chunk":
                    if (!string.IsNullOrEmpty(evt.Text))
                    {
                        accumulatedText.Append(evt.Text);
                        // Forward chunk immediately to channel as streaming activity
                        await SendStreamingChunkAsync(turnContext, accumulatedText.ToString(), cancellationToken);
                    }
                    break;

                case "done":
                    finalText = evt.Text ?? accumulatedText.ToString();
                    // Send final complete message
                    await SendFinalMessageAsync(turnContext, finalText, evt.Citations, cancellationToken);
                    return finalText;

                case "error":
                    _logger.LogError("Customer stream error: {Code} - {Message}", evt.Code, evt.Message);
                    await SendErrorToChannel(turnContext, evt.Message ?? "Agent encountered an error", cancellationToken);
                    return null;

                default:
                    _logger.LogDebug("Unknown SSE event type: {Type}", evt.Type);
                    break;
            }
        }

        // Stream ended without a "done" event — use accumulated text
        if (accumulatedText.Length > 0)
        {
            finalText = accumulatedText.ToString();
            await SendFinalMessageAsync(turnContext, finalText, null, cancellationToken);
        }

        return finalText;
    }

    private static async Task SendStreamingChunkAsync(
        Builder.ITurnContext turnContext,
        string accumulatedText,
        CancellationToken cancellationToken)
    {
        // Send as a streaming/typing activity with accumulated text
        // Channels that support progressive rendering will show this
        var activity = new Activity
        {
            Type = ActivityTypes.Typing,
            Text = accumulatedText,
        };
        await turnContext.SendActivityAsync(activity, cancellationToken);
    }

    private static async Task SendFinalMessageAsync(
        Builder.ITurnContext turnContext,
        string text,
        List<CitationInfo>? citations,
        CancellationToken cancellationToken)
    {
        var activity = MessageFactory.Text(text);

        if (citations?.Count > 0)
        {
            activity.Entities ??= new List<Entity>();
            foreach (var citation in citations)
            {
                var entity = new Entity
                {
                    Type = "https://schema.org/Claim",
                };
                entity.Properties["name"] = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(citation.Title));
                entity.Properties["url"] = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(citation.Url));
                activity.Entities.Add(entity);
            }
        }

        await turnContext.SendActivityAsync(activity, cancellationToken);
    }

    private static async Task SendErrorToChannel(
        Builder.ITurnContext turnContext,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(
            MessageFactory.Text($"⚠️ {errorMessage}"),
            cancellationToken);
    }
}
