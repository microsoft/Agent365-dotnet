// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// Non-streaming response from the customer's webhook.
/// </summary>
public sealed class TurnResponse
{
    /// <summary>
    /// Reply text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Text format: plain, markdown, or xml.
    /// </summary>
    [JsonPropertyName("textFormat")]
    public string? TextFormat { get; set; }

    /// <summary>
    /// Attachments (cards, images).
    /// </summary>
    [JsonPropertyName("attachments")]
    public List<AttachmentInfo>? Attachments { get; set; }

    /// <summary>
    /// Citations for the response.
    /// </summary>
    [JsonPropertyName("citations")]
    public List<CitationInfo>? Citations { get; set; }

    /// <summary>
    /// Suggested follow-up actions.
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<string>? SuggestedActions { get; set; }
}

/// <summary>
/// Citation reference in a response.
/// </summary>
public sealed class CitationInfo
{
    /// <summary>
    /// Citation title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Citation URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Cited content snippet.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
