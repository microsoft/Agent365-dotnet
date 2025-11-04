// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Common;
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
        public const string OperationName = "invoke_agent";

        /// <summary>
        /// Creates and starts a new scope for agent invocation tracing.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation including endpoint, agent information, and conversation context.</param>
        /// <param name="tenantDetails"></param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <returns>A new InvokeAgentScope instance.</returns>
        public static InvokeAgentScope Start(
            InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request = null, AgentDetails? callerAgentDetails = null, CallerDetails? callerDetails = null, string? conversationId = null) => new InvokeAgentScope(invokeAgentDetails, tenantDetails, request, callerAgentDetails, callerDetails, conversationId);

        private InvokeAgentScope(InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request, AgentDetails? callerAgentDetails, CallerDetails? callerDetails, string? conversationId)
            : base(
                ActivityKind.Client,
                invokeAgentDetails.Details,
                tenantDetails,
                OperationName,
                string.IsNullOrWhiteSpace(invokeAgentDetails.Details.AgentName)
                    ? OperationName
                    : $"invoke_agent {invokeAgentDetails.Details.AgentName}")
        {
            // Build all attributes using the shared builder
            var attributes = InvokeAgentAttributesBuilder.BuildAttributes(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId);

            // Apply all attributes to the activity
            RecordAttributes(attributes);
        }

        // TODO: Refactor these methods to use InvokeAgentAttributesBuilder
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
    }
}