// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Extensions.SemanticKernel;

/// <summary>
/// Contains constants for operation names, tag names, and activity source names used in SemanticKernel and OpenAI tracing.
/// </summary>
internal static class SemanticKernelTelemetryConstants
{
    // Operation Names
    public const string InvokeAgentOperation = "invoke_agent";
    public const string ExecuteToolOperation = "execute_tool";
    public const string ChatCompletionsOperation = "chat.completions";
    public const string ChatOperation = "chat";

    // Activity Source Names
    public const string SemanticKernelSource = "Microsoft.SemanticKernel";
    public const string SemanticKernelSourceWildcard = "Microsoft.SemanticKernel*";
    public const string AzureAISourceWildcard = "Azure.AI.*";

    // Configuration Keys
    public const string SuppressInvokeAgentInputConfigKey = "SuppressInvokeAgentInput";

    // Event Tag Keys
    public const string EventContentTag = "gen_ai.event.content";
}
