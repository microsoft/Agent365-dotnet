// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Sidecar.Messaging;

/// <summary>
/// Simplified turn payload delivered to the customer's webhook.
/// Translates from the full Activity Protocol to a language-agnostic JSON contract.
/// </summary>
public sealed class TurnPayload
{
    /// <summary>
    /// Unique ID for this turn, used to correlate outbound replies.
    /// </summary>
    [JsonPropertyName("turnId")]
    public string TurnId { get; set; } = string.Empty;

    /// <summary>
    /// Activity type: message, event, conversationUpdate, invoke.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Conversation identifier.
    /// </summary>
    [JsonPropertyName("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Source channel (e.g., "msteams", "webchat", "m365copilot").
    /// </summary>
    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// The sender.
    /// </summary>
    [JsonPropertyName("from")]
    public AccountInfo From { get; set; } = new();

    /// <summary>
    /// The recipient (the agent).
    /// </summary>
    [JsonPropertyName("recipient")]
    public AccountInfo Recipient { get; set; } = new();

    /// <summary>
    /// Message text (for message activities).
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// File/card attachments.
    /// </summary>
    [JsonPropertyName("attachments")]
    public List<AttachmentInfo>? Attachments { get; set; }

    /// <summary>
    /// ISO 8601 timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Members added (conversationUpdate only).
    /// </summary>
    [JsonPropertyName("membersAdded")]
    public List<AccountInfo>? MembersAdded { get; set; }

    /// <summary>
    /// Members removed (conversationUpdate only).
    /// </summary>
    [JsonPropertyName("membersRemoved")]
    public List<AccountInfo>? MembersRemoved { get; set; }

    /// <summary>
    /// Event name (event activities only).
    /// </summary>
    [JsonPropertyName("eventName")]
    public string? EventName { get; set; }

    /// <summary>
    /// Event value payload (event activities only).
    /// </summary>
    [JsonPropertyName("eventValue")]
    public object? EventValue { get; set; }

    /// <summary>
    /// Invoke name (invoke activities only).
    /// </summary>
    [JsonPropertyName("invokeName")]
    public string? InvokeName { get; set; }

    /// <summary>
    /// Invoke value payload (invoke activities only).
    /// </summary>
    [JsonPropertyName("invokeValue")]
    public object? InvokeValue { get; set; }

    /// <summary>
    /// Raw channel-specific data (pass-through).
    /// </summary>
    [JsonPropertyName("channelData")]
    public object? ChannelData { get; set; }
}

/// <summary>
/// Channel account information.
/// </summary>
public sealed class AccountInfo
{
    /// <summary>
    /// Account ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// AAD Object ID (if available).
    /// </summary>
    [JsonPropertyName("aadObjectId")]
    public string? AadObjectId { get; set; }
}

/// <summary>
/// Attachment information.
/// </summary>
public sealed class AttachmentInfo
{
    /// <summary>
    /// Content type (MIME).
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// URL to download the attachment content.
    /// </summary>
    [JsonPropertyName("contentUrl")]
    public string? ContentUrl { get; set; }

    /// <summary>
    /// Inline content (for cards).
    /// </summary>
    [JsonPropertyName("content")]
    public object? Content { get; set; }

    /// <summary>
    /// Attachment name/filename.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
