// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

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
        /// <returns>A new OpenTelemetryScope instance if telemetry is enabled, otherwise null.</returns>
        public static InvokeAgentScope? Start(
            InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request = null) => new InvokeAgentScope(invokeAgentDetails, tenantDetails, request);

        private InvokeAgentScope(InvokeAgentDetails invokeAgentDetails, TenantDetails tenantDetails, Request? request)
            : base(
                ActivityKind.Client,
                invokeAgentDetails.Details,
                tenantDetails,
                OperationName,
                string.IsNullOrWhiteSpace(invokeAgentDetails.Details.AgentName)
                    ? OperationName
                    : $"invoke_agent {invokeAgentDetails.Details.AgentName}")
        {
            var (endpoint, _, sessionId) = invokeAgentDetails;


            SetTagMaybe(SessionIdKey, sessionId);
            SetTagMaybe(ServerAddressKey, endpoint.Host);
            SetTagMaybe(GenAiRequestContentKey, request?.Content);

            // Only record port if it is different from 443
            if (endpoint.Port != 443)
            {
                SetTagMaybe(ServerPortKey, endpoint.Port);
            }
        }
        
        /// <summary>
        /// Records response information for telemetry tracking.
        /// </summary>
        public void RecordResponse(string response)
        {
            SetTagMaybe(GenAiEventContent, response);
        }

        /// <summary>
        /// Records the input messages for telemetry tracking.
        /// </summary>
        public void RecordInputMessages(string[] messages)
        {
            SetTagMaybe(GenAiInputMessagesKey, string.Join(",", messages));
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// </summary>
        public void RecordOutputMessages(string[] messages)
        {
            SetTagMaybe(GenAiOutputMessagesKey, string.Join(",", messages));
        }
    }
}