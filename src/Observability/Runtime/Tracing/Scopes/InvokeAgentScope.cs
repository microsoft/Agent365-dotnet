// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent invocation operations.
    /// </summary>
    public sealed class InvokeAgentScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for agent invocation tracing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see href="https://learn.microsoft.com/microsoft-agent-365/developer/observability?tabs=dotnet#agent-invocation">Learn more about Agent Invocation</see>
        /// </para>
        /// </remarks>
        public const string OperationName = "invoke_agent";

        /// <summary>
        /// Creates and starts a new scope for agent invocation tracing.
        /// </summary>
        /// <param name="request">The request details for the invocation.</param>
        /// <param name="scopeDetails">Scope-level configuration (endpoint).</param>
        /// <param name="agentDetails">The details of the agent being invoked.</param>
        /// <param name="callerDetails">Optional composite caller details (human user and/or calling agent for A2A scenarios).</param>
        /// <param name="spanDetails">Optional span configuration (parent context, timing, kind, span links).</param>
        /// <param name="threatDiagnosticsSummary">Optional threat diagnostics summary containing security-related information about blocked actions.</param>
        /// <returns>A new InvokeAgentScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set (i.e., not <c>null</c>) for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="request"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        ///   <item><paramref name="callerDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static InvokeAgentScope Start(
            Request request,
            InvokeAgentScopeDetails scopeDetails,
            AgentDetails agentDetails,
            CallerDetails? callerDetails = null,
            SpanDetails? spanDetails = null,
            ThreatDiagnosticsSummary? threatDiagnosticsSummary = null) => new InvokeAgentScope(request, scopeDetails, agentDetails, callerDetails, spanDetails, threatDiagnosticsSummary);

        private InvokeAgentScope(
            Request request,
            InvokeAgentScopeDetails scopeDetails,
            AgentDetails agentDetails,
            CallerDetails? callerDetails,
            SpanDetails? spanDetails,
            ThreatDiagnosticsSummary? threatDiagnosticsSummary)
            : base(
                operationName: OperationName,
                activityName: string.IsNullOrWhiteSpace(agentDetails?.AgentName)
                    ? OperationName
                    : $"invoke_agent {agentDetails!.AgentName}",
                agentDetails: agentDetails!,
                spanDetails: new SpanDetails(spanDetails?.SpanKind ?? ActivityKind.Client, spanDetails?.ParentContext, spanDetails?.StartTime, spanDetails?.EndTime, spanDetails?.SpanLinks),
                userDetails: callerDetails?.UserDetails)
        {
            SetTagMaybe(OpenTelemetryConstants.SessionIdKey, request?.SessionId);
            SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, request?.ConversationId);
            SetTagMaybe(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, threatDiagnosticsSummary?.ToJson());

            var endpoint = scopeDetails?.Endpoint;
            if (endpoint != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
                }
            }

            // Set request metadata
            if (request?.Channel != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ChannelNameKey, request.Channel.Name);
                SetTagMaybe(OpenTelemetryConstants.ChannelLinkKey, request.Channel.Link);
            }

            if (request?.Content != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiInputMessagesKey, request.Content);
            }

            // Set caller details tags
            if (callerDetails != null)
            {
                var callerAgentDetails = callerDetails.CallerAgentDetails;
                if (callerAgentDetails != null)
                {
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentNameKey, callerAgentDetails.AgentName);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentIdKey, callerAgentDetails.AgentId);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentBlueprintIdKey, callerAgentDetails.AgentBlueprintId);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentAUIDKey, callerAgentDetails.AgenticUserId);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentEmailKey, callerAgentDetails.AgenticUserEmail);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
                    SetTagMaybe(OpenTelemetryConstants.CallerAgentVersionKey, callerAgentDetails.AgentVersion);
                }
            }
        }

        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            this.RecordOutputMessages(messages: new string[] { response });
        }

        /// <summary>
        /// Records the input messages for telemetry tracking.
        /// </summary>
        public void RecordInputMessages(string[] messages)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", messages));
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// </summary>
        public void RecordOutputMessages(string[] messages)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", messages));
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