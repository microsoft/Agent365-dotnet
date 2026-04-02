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
        /// <param name="request">Request details for the tool execution.</param>
        /// <param name="details">Details of the tool call (name, args, type, call ID, description, endpoint).</param>
        /// <param name="agentDetails">Information about the agent executing the tool (service, version, identifiers).</param>
        /// <param name="userDetails">Optional human user details.</param>
        /// <param name="spanDetails">Optional span configuration (parent context, timing, kind, span links).</param>
        /// <param name="threatDiagnosticsSummary">Optional threat diagnostics summary containing security-related information about blocked actions.</param>
        /// <returns>A new ExecuteToolScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="details"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static ExecuteToolScope Start(Request request, ToolCallDetails details, AgentDetails agentDetails, UserDetails? userDetails = null, SpanDetails? spanDetails = null, ThreatDiagnosticsSummary? threatDiagnosticsSummary = null) => new ExecuteToolScope(request, details, agentDetails, userDetails, spanDetails, threatDiagnosticsSummary);

        private ExecuteToolScope(Request request, ToolCallDetails details, AgentDetails agentDetails, UserDetails? userDetails, SpanDetails? spanDetails, ThreatDiagnosticsSummary? threatDiagnosticsSummary)
            : base(
                operationName: OperationName,
                activityName: $"{OperationName} {details.ToolName}",
                agentDetails: agentDetails,
                spanDetails: new SpanDetails(spanDetails?.SpanKind ?? ActivityKind.Internal, spanDetails?.ParentContext, spanDetails?.StartTime, spanDetails?.EndTime, spanDetails?.SpanLinks),
                userDetails: userDetails)
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint, toolServerName) = details;
            SetTagMaybe(OpenTelemetryConstants.GenAiToolNameKey, toolName);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolTypeKey, toolType);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolCallIdKey, toolCallId);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolDescriptionKey, description);
            SetTagMaybe(OpenTelemetryConstants.GenAiToolServerNameKey, toolServerName);
            SetTagMaybe(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, threatDiagnosticsSummary?.ToJson());
            SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, request?.ConversationId);

            if (endpoint != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
                }
            }

            if (request?.Channel != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ChannelNameKey, request.Channel.Name);
                SetTagMaybe(OpenTelemetryConstants.ChannelLinkKey, request.Channel.Link);
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