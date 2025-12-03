// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI tool execution operations.
    /// </summary>
    /// <remarks>
    /// <see href="https://learn.microsoft.com/microsoft-agent-365/developer/observability?tabs=dotnet#tool-execution">Learn more about tool execution</see>
    /// </remarks>
    public sealed class ExecuteToolScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for tool execution tracing.
        /// </summary>
        public const string OperationName = "execute_tool";

        /// <summary>
        /// Creates and starts a new scope for tool execution tracing.
        /// </summary>
        /// <param name="details">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent executing the tool.</param>
        /// <param name="tenantDetails">The tenant details for the tool execution.</param>
        /// <param name="parentId">Optional parent activity ID.</param>
        /// <returns>A new ExecuteToolScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="details"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        ///   <item><paramref name="tenantDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static ExecuteToolScope Start(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null) => new ExecuteToolScope(details, agentDetails, tenantDetails, parentId);

        private ExecuteToolScope(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null)
            : base(
                ActivityKind.Internal,
                agentDetails,
                tenantDetails,
                OperationName,
                $"{OperationName} {details.ToolName}",
                parentId: parentId)
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint) = details;
            SetTagMaybe(OpenTelemetryConstants.GenAiToolNameKey, toolName);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolTypeKey, toolType);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolCallIdKey, toolCallId);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolDescriptionKey, description);

            if (endpoint !=null)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint.Port);
                }
            }
        }
        
        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiEventContent, response);
        }
    }
}