// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;

/// <summary>
/// Model for gen_ai.agent.invocation_input element.
/// </summary>
public class GenAiInvocationInput
{
    /// <summary>
    /// The role of the invocation input, such as "user" or "assistant".
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// The content of the invocation input.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// The name associated with the invocation input.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}