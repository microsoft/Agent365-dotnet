// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

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
        /// <para>
        /// <b>Note:</b> While <paramref name="request"/> and <paramref name="callerDetails"/> are optional in the API, they must be provided (not <c>null</c>) to meet certification requirements.
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
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
            var (endpoint, _, sessionId) = invokeAgentDetails;

            SetTagMaybe(OpenTelemetryConstants.SessionIdKey, sessionId);
            SetTagMaybe(OpenTelemetryConstants.ServerAddressKey, endpoint?.Host);
            SetTagMaybe(OpenTelemetryConstants.GenAiChannelNameKey, request?.SourceMetadata?.Name);
            SetTagMaybe(OpenTelemetryConstants.GenAiChannelLinkKey, request?.SourceMetadata?.Description);
            SetTagMaybe(OpenTelemetryConstants.GenAiExecutionTypeKey, request?.ExecutionType.ToString());
            SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Only record port if it is different from 443
            if (endpoint?.Port != 443)
            {
                SetTagMaybe(OpenTelemetryConstants.ServerPortKey, endpoint?.Port);
            }

            // Set caller details tags
            if (callerDetails != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerClientIpKey, callerDetails.CallerClientIP?.ToString());
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
            }

            // Set caller agent details tags
            if (callerAgentDetails != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentNameKey, callerAgentDetails.AgentName);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentIdKey, callerAgentDetails.AgentId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentApplicationIdKey, callerAgentDetails.AgentBlueprintId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentAUIDKey, callerAgentDetails.AgentAUID);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentUPNKey, callerAgentDetails.AgentUPN);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentTenantKey, callerAgentDetails.TenantId);
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentTypeKey, callerAgentDetails.AgentType?.ToString());
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentClientIpKey, callerAgentDetails.AgentClientIP?.ToString());
                SetTagMaybe(OpenTelemetryConstants.GenAiCallerAgentPlatformIdKey, callerAgentDetails.AgentPlatformId);
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
    }
}