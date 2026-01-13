// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent output operations.
    /// </summary>
    public sealed class OutputScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for output tracing.
        /// </summary>
        public const string OperationName = "output_messages";

        /// <summary>
        /// Creates and starts a new scope for output tracing.
        /// </summary>
        /// <param name="agentDetails">Information about the agent sending the output (service, version, identifiers).</param>
        /// <param name="tenantDetails">Tenant context used for telemetry enrichment and correlation.</param>
        /// <param name="response">Optional response content containing output messages.</param>
        /// <param name="callerDetails">Optional details about the non-agentic caller.</param>
        /// <param name="conversationId">Optional conversation or session correlation ID.</param>
        /// <param name="sessionId">Optional session identifier.</param>
        /// <param name="sessionDescription">Optional session description.</param>
        /// <param name="parentId">Optional parent Activity ID used to link this span to an upstream operation.</param>
        /// <param name="sourceMetadata">Optional metadata describing the source of the call for observability.</param>
        /// <param name="executionType">Optional execution type describing the request.</param>
        /// <returns>A new OutputScope instance.</returns>
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
        public static OutputScope Start(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Response? response = null,
            CallerDetails? callerDetails = null,
            string? conversationId = null,
            string? sessionId = null,
            string? sessionDescription = null,
            string? parentId = null,
            SourceMetadata? sourceMetadata = null,
            ExecutionType? executionType = null) => new OutputScope(agentDetails, tenantDetails, response, callerDetails, conversationId, sessionId, sessionDescription, parentId, sourceMetadata, executionType);

        private OutputScope(
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            Response? response,
            CallerDetails? callerDetails,
            string? conversationId,
            string? sessionId,
            string? sessionDescription,
            string? parentId,
            SourceMetadata? sourceMetadata,
            ExecutionType? executionType)
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
                sourceMetadata: sourceMetadata)
        {
            SetTagMaybe(OpenTelemetryConstants.SessionIdKey, sessionId);
            SetTagMaybe(OpenTelemetryConstants.SessionDescriptionKey, sessionDescription);
            SetTagMaybe(OpenTelemetryConstants.GenAiExecutionTypeKey, executionType?.ToString());

            // Set caller details tags
            if (callerDetails != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerClientIpKey, callerDetails.CallerClientIP?.ToString());
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
            }

            // Set output messages
            if (response?.Content != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, response.Content);
            }
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// </summary>
        /// <param name="messages">Array of output messages to record.</param>
        public void RecordOutputMessages(string[] messages)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", messages));
        }
    }
}
