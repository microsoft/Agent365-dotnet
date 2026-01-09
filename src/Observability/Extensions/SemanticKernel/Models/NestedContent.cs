// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;

/// <summary>
/// Represents a nested content structure that may appear within a MessageContent's Content property.
/// Used when Content is a JSON object with contentType and content fields.
/// </summary>
public class NestedContent
{
    /// <summary>
    /// The type of the content (e.g., "Text").
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    /// <summary>
    /// The actual content value.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
