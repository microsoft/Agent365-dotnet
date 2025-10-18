// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

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
        /// <returns>A new ExecuteToolScope instance if telemetry is enabled, otherwise null.</returns>
        public static ExecuteToolScope? Start(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails) => new ExecuteToolScope(details, agentDetails, tenantDetails);

        private ExecuteToolScope(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails)
            : base(
                ActivityKind.Internal,
                agentDetails,
                tenantDetails,
                OperationName,
                $"{OperationName} {details.ToolName}")
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint) = details;
            SetTagMaybe(GenAiToolNameKey, toolName);
            SetTagMaybe(GenAiToolArgumentsKey, arguments);
            SetTagMaybe(GenAiToolTypeKey, toolType);
            SetTagMaybe(GenAiToolCallIdKey, toolCallId);
            SetTagMaybe(GenAiToolDescriptionKey, description);

            if(endpoint !=null)
            {
                SetTagMaybe(ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    SetTagMaybe(ServerPortKey, endpoint.Port);
                }
            }        
            
        }
        
        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            SetTagMaybe(GenAiEventContent, response);
        }
    }
}