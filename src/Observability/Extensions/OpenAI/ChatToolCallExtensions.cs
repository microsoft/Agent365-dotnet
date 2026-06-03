// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using OpenAI.Chat;

namespace Microsoft.Agents.A365.Observability.Extensions.OpenAI;

/// <summary>
/// Extension methods for ChatToolCall.
/// </summary>
public static class ChatToolCallExtensions
{
    /// <summary>
    /// Starts an ExecuteToolScope for the given ChatToolCall for OpenTelemetry tracing.
    /// </summary>
    /// <param name="chatToolCall">The ChatToolCall instance.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>An ExecuteToolScope.</returns>
    public static ExecuteToolScope? Trace(this ChatToolCall chatToolCall, string agentId, string? tenantId = null)
    {
        var details = new ToolCallDetails(
            chatToolCall.FunctionName,
            chatToolCall.FunctionArguments?.ToString(),
            chatToolCall.Id,
            null,
            chatToolCall.Kind.ToString()
        );

        var agentDetails = new AgentDetails(agentId: agentId, tenantId: tenantId);
        return ExecuteToolScope.Start(new Request(), details, agentDetails);
    }
}
