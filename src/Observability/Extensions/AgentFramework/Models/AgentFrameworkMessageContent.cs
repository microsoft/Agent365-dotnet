// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Extensions.AgentFramework.Models;

/// <summary>
/// Represents the structure of a message as found in Agent Framework OpenTelemetry activity tags.
/// Used in the <c>gen_ai.input.messages</c> and <c>gen_ai.output.messages</c> tags.
/// </summary>
internal class AgentFrameworkMessageContent
{
    /// <summary>
    /// The role of the message, such as "user" or "assistant".
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// The parts of the message, each containing type and content.
    /// </summary>
    [JsonPropertyName("parts")]
    public List<AgentFrameworkMessagePart>? Parts { get; set; }

    /// <summary>
    /// The finish reason for the message (e.g., "stop").
    /// </summary>
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Represents a part of an Agent Framework message.
/// </summary>
internal class AgentFrameworkMessagePart
{
    /// <summary>
    /// The type of the part (e.g., "text").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// The content of the part.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
