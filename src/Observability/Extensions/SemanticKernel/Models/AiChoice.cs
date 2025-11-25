// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel.Models;

using System.Text.Json.Serialization;

internal sealed class AiChoice
{
    [JsonPropertyName("message")]
    public AiChoiceMessage? Message { get; set; }
}

internal sealed class AiChoiceMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<AiChoiceToolCall>? ToolCalls { get; set; }
}

internal sealed class AiChoiceToolCall
{
    [JsonPropertyName("function")]
    public AiChoiceFunction? Function { get; set; }
}

internal sealed class AiChoiceFunction
{
    [JsonPropertyName("arguments")]
    public AiChoiceArguments? Arguments { get; set; }
}

internal sealed class AiChoiceArguments
{
    [JsonPropertyName("messageBody")]
    public string? MessageBody { get; set; }
}