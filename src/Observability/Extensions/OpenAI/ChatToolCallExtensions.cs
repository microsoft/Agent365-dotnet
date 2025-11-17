// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
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
    /// <param name="agentId"></param>
    /// <param name="tenantId"></param>
    /// <returns>An ExecuteToolScope.</returns>
    public static ExecuteToolScope? Trace(this ChatToolCall chatToolCall, string agentId, Guid tenantId)
    {
        var details = new ToolCallDetails(
            chatToolCall.FunctionName,
            chatToolCall.FunctionArguments?.ToString(),
            chatToolCall.Id,
            null,
            chatToolCall.Kind.ToString()
        );

        var agentDetails = new AgentDetails(agentId);
        var tenentDetails = new TenantDetails(tenantId);
        return ExecuteToolScope.Start(details, agentDetails, tenentDetails);
    }
}
