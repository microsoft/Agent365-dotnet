// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;

/// <summary>
/// Represents the structure of a message as found in OpenTelemetry activity events and invocation input tags.
/// Used in the <c>gen_ai.agent.invocation_input</c> tag and <c>gen_ai.event.content</c> attribute.
/// </summary>
public class MessageContent
{
    /// <summary>
    /// The role of the message, such as "user" or "assistant".
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// The content of the event.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// The name associated with the message.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}