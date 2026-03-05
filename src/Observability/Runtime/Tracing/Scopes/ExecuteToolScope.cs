// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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
        /// <param name="details">Details of the tool call (name, args, type, call ID, description, endpoint).</param>
        /// <param name="agentDetails">Information about the agent executing the tool (service, version, identifiers).</param>
        /// <param name="tenantDetails">Tenant context used for telemetry enrichment and correlation.</param>
        /// <param name="parentId">Optional parent Activity ID used to link this span to an upstream operation.</param>
        /// <param name="conversationId">Optional conversation or session correlation ID for the tool execution.</param>
        /// <param name="sourceMetadata">Optional metadata describing the source of the call (e.g., component, file, line) for observability.</param>
        /// <param name="threatDiagnosticsSummary">Optional threat diagnostics summary containing security-related information about blocked actions.</param>
        /// <param name="callerDetails">Optional details about the non-agentic caller.</param>
        /// <param name="startTime">Optional explicit start time. Useful when recording a tool call after execution has already completed.</param>
        /// <param name="endTime">Optional explicit end time. When provided, the span will use this timestamp when disposed instead of the current wall-clock time.</param>
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
        public static ExecuteToolScope Start(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null, string? conversationId = null, SourceMetadata? sourceMetadata = null, ThreatDiagnosticsSummary? threatDiagnosticsSummary = null, CallerDetails? callerDetails = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null) => new ExecuteToolScope(details, agentDetails, tenantDetails, parentId, conversationId, sourceMetadata, threatDiagnosticsSummary, callerDetails, startTime, endTime);

        private ExecuteToolScope(ToolCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null, string? conversationId = null, SourceMetadata? sourceMetadata = null, ThreatDiagnosticsSummary? threatDiagnosticsSummary = null, CallerDetails? callerDetails = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null)
            : base(
                kind: ActivityKind.Internal,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                operationName: OperationName,
                activityName: $"{OperationName} {details.ToolName}",
                startTime: startTime,
                endTime: endTime,
                parentId: parentId,
                conversationId: conversationId,
                sourceMetadata: sourceMetadata,
                callerDetails: callerDetails)
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint, toolServerName) = details;
            SetTagMaybe(OpenTelemetryConstants.GenAiToolNameKey, toolName);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolTypeKey, toolType);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolCallIdKey, toolCallId);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolDescriptionKey, description);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolServerNameKey, toolServerName);
            SetTagMaybe(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, threatDiagnosticsSummary?.ToJson());

            if (endpoint !=null)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
                }
            }
        }
        
        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiToolCallResultKey, response);
        }

        /// <summary>
        /// Records threat diagnostics summary for telemetry tracking.
        /// </summary>
        /// <param name="threatDiagnosticsSummary">The threat diagnostics summary containing security-related information about blocked actions.</param>
        public void RecordThreatDiagnosticsSummary(ThreatDiagnosticsSummary threatDiagnosticsSummary)
        {
            SetTagMaybe(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, threatDiagnosticsSummary.ToJson());
        }
    }
}