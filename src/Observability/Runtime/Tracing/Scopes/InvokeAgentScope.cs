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
        /// <param name="invokeAgentDetails">The details of the agent invocation including endpoint, agent information, and conversation context.</param>
        /// <param name="tenantDetails">The tenant details for the agent invocation.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent. Only used for agent-to-agent invocation.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <param name="threatDiagnosticsSummary">Optional threat diagnostics summary containing security-related information about blocked actions.</param>
        /// <param name="startTime">Optional explicit start time. Useful when recording an agent invocation after execution has already completed.</param>
        /// <param name="endTime">Optional explicit end time. When provided, the span will use this timestamp when disposed instead of the current wall-clock time.</param>
        /// <param name="parentContext">Optional parent <see cref="System.Diagnostics.ActivityContext"/> used to link this span to an upstream operation.
        /// Use <see cref="TraceContextHelper.ExtractContextFromHeaders"/> to obtain an <see cref="System.Diagnostics.ActivityContext"/> from HTTP headers containing a W3C traceparent.</param>
        /// <param name="spanKind">Optional span kind override. Defaults to <see cref="ActivityKind.Client"/>. Use <see cref="ActivityKind.Server"/> when the agent is receiving an inbound request.</param>
        /// <returns>A new InvokeAgentScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set (i.e., not <c>null</c>) for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="invokeAgentDetails"/></item>
        ///   <item><paramref name="tenantDetails"/></item>
        ///   <item><paramref name="request"/></item>
        ///   <item><paramref name="callerDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>Note:</b> While <paramref name="request"/> and <paramref name="callerDetails"/> are optional in the API, they must be provided (not <c>null</c>) to meet certification requirements.
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static InvokeAgentScope Start(
            InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request = null, AgentDetails? callerAgentDetails = null, CallerDetails? callerDetails = null, string? conversationId = null, ThreatDiagnosticsSummary? threatDiagnosticsSummary = null, DateTimeOffset? startTime = null, DateTimeOffset? endTime = null, ActivityContext? parentContext = null, ActivityKind? spanKind = null) => new InvokeAgentScope(invokeAgentDetails, tenantDetails, request, callerAgentDetails, callerDetails, conversationId, threatDiagnosticsSummary, startTime, endTime, parentContext, spanKind);

        private InvokeAgentScope(InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request, AgentDetails? callerAgentDetails, CallerDetails? callerDetails, string? conversationId, ThreatDiagnosticsSummary? threatDiagnosticsSummary, DateTimeOffset? startTime, DateTimeOffset? endTime, ActivityContext? parentContext, ActivityKind? spanKind)
            : base(
                kind: spanKind ?? ActivityKind.Client,
                agentDetails: invokeAgentDetails.Details,
                tenantDetails: tenantDetails,
                operationName: OperationName,
                activityName: string.IsNullOrWhiteSpace(invokeAgentDetails.Details.AgentName)
                    ? OperationName
                    : $"invoke_agent {invokeAgentDetails.Details.AgentName}",
                startTime: startTime,
                endTime: endTime,
                parentContext: parentContext,                conversationId: conversationId,
                channel: request?.Channel,
                callerDetails: callerDetails)
        {
            var (endpoint, _, sessionId) = invokeAgentDetails;

            SetTagMaybe(OpenTelemetryConstants.SessionIdKey, sessionId);
            SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint?.Host);
            SetTagMaybe(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, threatDiagnosticsSummary?.ToJson());

            // Only record port if it is different from 443
            if (endpoint != null && endpoint.Port != 443)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
            }

            // Set caller agent details tags
            if (callerAgentDetails != null)
            {
                SetTagMaybe(OpenTelemetryConstants.CallerAgentNameKey, callerAgentDetails.AgentName);
                SetTagMaybe(OpenTelemetryConstants.CallerAgentIdKey, callerAgentDetails.AgentId);
                SetTagMaybe(OpenTelemetryConstants.CallerAgentBlueprintIdKey, callerAgentDetails.AgentBlueprintId);
                SetTagMaybe(OpenTelemetryConstants.CallerAgentAUIDKey, callerAgentDetails.AgentAUID);
                SetTagMaybe(OpenTelemetryConstants.CallerAgentEmailKey, callerAgentDetails.AgentUPN);
                SetTagMaybe(OpenTelemetryConstants.CallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
            }

            // Set input messages 
            if (request?.Content != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiInputMessagesKey, request.Content);
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