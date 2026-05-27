// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Sidecar.Tooling;

/// <summary>
/// Request to invoke an MCP tool.
/// </summary>
public sealed class ToolInvocationRequest
{
    /// <summary>
    /// Tool input arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public Dictionary<string, object> Arguments { get; set; } = new();

    /// <summary>
    /// Optional conversation context to pass to the MCP server.
    /// </summary>
    [JsonPropertyName("conversationContext")]
    public ConversationContextInfo? ConversationContext { get; set; }
}

/// <summary>
/// Conversation context passed to MCP servers.
/// </summary>
public sealed class ConversationContextInfo
{
    /// <summary>
    /// Conversation ID.
    /// </summary>
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    /// <summary>
    /// Channel ID.
    /// </summary>
    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }

    /// <summary>
    /// Sub-channel ID.
    /// </summary>
    [JsonPropertyName("subChannelId")]
    public string? SubChannelId { get; set; }

    /// <summary>
    /// User message text.
    /// </summary>
    [JsonPropertyName("userMessage")]
    public string? UserMessage { get; set; }
}

/// <summary>
/// Response from invoking an MCP tool.
/// </summary>
public sealed class ToolInvocationResponse
{
    /// <summary>
    /// Tool execution result.
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    /// <summary>
    /// Whether the tool invocation resulted in an error.
    /// </summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    /// <summary>
    /// Error message if the invocation failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
