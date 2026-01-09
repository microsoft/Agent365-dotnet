// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

/// <summary>
/// Configuration options for OpenAI span processing.
/// </summary>
public class OpenAISpanProcessorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to send LLM prompt content in InvokeAgent scopes.
    /// When set to false, the gen_ai.input.messages tag will be removed from InvokeAgent spans
    /// to prevent sensitive prompt data from being recorded in telemetry.
    /// Defaults to true for backward compatibility.
    /// </summary>
    public bool SendPromptInInvokeAgentScopes { get; set; } = true;
}
