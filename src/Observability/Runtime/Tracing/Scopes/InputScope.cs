// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent input operations.
    /// </summary>
    public sealed class InputScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for input tracing.
        /// </summary>
        public const string OperationName = "input_messages";

        /// <summary>
        /// Creates and starts a new scope for input tracing.
        /// </summary>
        /// <param name="agentDetails">Information about the agent receiving the input (service, version, identifiers).</param>
        /// <param name="tenantDetails">Tenant context used for telemetry enrichment and correlation.</param>
        /// <param name="request">Optional request content containing input messages and execution context.</param>
        /// <param name="callerDetails">Optional details about the non-agentic caller.</param>
        /// <param name="conversationId">Optional conversation or session correlation ID.</param>
        /// <param name="sessionId">Optional session identifier.</param>
        /// <param name="sessionDescription">Optional session description.</param>
        /// <param name="parentId">Optional parent Activity ID used to link this span to an upstream operation.</param>
        /// <param name="threatDiagnosticsSummary">Optional threat diagnostics summary containing security-related information about blocked actions.</param>
        /// <returns>A new InputScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="agentDetails"/></item>
        ///   <item><paramref name="tenantDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static InputScope Start(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Request? request = null,
            CallerDetails? callerDetails = null,
            string? conversationId = null,
            string? sessionId = null,
            string? sessionDescription = null,
            string? parentId = null,
            ThreatDiagnosticsSummary? threatDiagnosticsSummary = null) => new InputScope(agentDetails, tenantDetails, request, callerDetails, conversationId, sessionId, sessionDescription, parentId);

        private InputScope(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Request? request,
            CallerDetails? callerDetails,
            string? conversationId,
            string? sessionId,
            string? sessionDescription,
            string? parentId)
            : base(
                kind: ActivityKind.Client,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                operationName: OperationName,
                activityName: string.IsNullOrWhiteSpace(agentDetails.AgentName)
                    ? OperationName
                    : $"{OperationName} {agentDetails.AgentName}",
                parentId: parentId,
                conversationId: conversationId,
                sourceMetadata: request?.SourceMetadata)
        {
            SetTagMaybe(OpenTelemetryConstants.SessionIdKey, sessionId);
            SetTagMaybe(OpenTelemetryConstants.SessionDescriptionKey, sessionDescription);
            SetTagMaybe(OpenTelemetryConstants.GenAiExecutionTypeKey, request?.ExecutionType?.ToString());

            // Set caller details tags
            if (callerDetails != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerClientIpKey, callerDetails.CallerClientIP?.ToString());
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
            }

            // Set input messages
            if (request?.Content != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiInputMessagesKey, request.Content);
            }
        }

        /// <summary>
        /// Records the input messages for telemetry tracking.
        /// </summary>
        /// <param name="messages">Array of input messages to record.</param>
        public void RecordInputMessages(string[] messages)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", messages));
        }
    }
}
