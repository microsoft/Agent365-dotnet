// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// A single Server-Sent Event in the streaming response protocol.
/// </summary>
public sealed class StreamingEvent
{
    /// <summary>
    /// Event type: typing, chunk, done, or error.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Text content (for chunk and done events).
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Citations (only with done event).
    /// </summary>
    [JsonPropertyName("citations")]
    public List<CitationInfo>? Citations { get; set; }

    /// <summary>
    /// Error message (only with error event).
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Error code (only with error event).
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
