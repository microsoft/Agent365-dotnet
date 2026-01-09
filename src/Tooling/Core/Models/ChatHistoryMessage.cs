// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Tooling.Models;

/// <summary>
/// Represents a single message in the chat history.
/// </summary>
public class ChatHistoryMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatHistoryMessage"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the chat message.</param>
    /// <param name="role">The role of the message sender (e.g., "user", "system").</param>
    /// <param name="content">The content of the chat message.</param>
    /// <param name="timestamp">The timestamp of when the message was sent.</param>
    public ChatHistoryMessage(string id, string role, string content, DateTimeOffset timestamp)
    {
        this.Id = id;
        this.Role = role;
        this.Content = content;
        this.Timestamp = timestamp;
    }

    /// <summary>
    /// Gets or sets the unique identifier for the chat message.
    /// </summary>
    [JsonPropertyName("id")]
    [Required]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender (e.g., "user", "system").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the chat message.
    /// </summary>
    [JsonPropertyName("content")]
    [Required]
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of when the message was sent.
    /// </summary>
    [JsonPropertyName("timestamp")]
    [Required]
    public DateTimeOffset Timestamp { get; set; }
}