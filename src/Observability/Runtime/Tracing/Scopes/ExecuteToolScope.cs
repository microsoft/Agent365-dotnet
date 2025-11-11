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
    public sealed class ExecuteToolScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for tool execution tracing.
        /// </summary>
        public const string OperationName = "execute_tool";

        /// <summary>
        /// Creates and starts a new scope for tool execution tracing.
        /// </summary>
        /// <returns>A new ExecuteToolScope instance.</returns>
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
            SetTagMaybe(OpenTelemetryConstants.GenAiOperationNameKey, OperationName);
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