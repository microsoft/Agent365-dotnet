// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an InvokeAgentData instance
    /// </summary>
    public class InvokeAgentDataBuilder : BaseDataBuilder<InvokeAgentData>
    {
        /// <summary>
        /// Builds complete data for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="conversationId">The required conversation ID for the agent invocation.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the telemetry.</param>
        /// <param name="outputMessages">Optional output messages to include in the telemetry.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <returns>An InvokeAgentData object containing all telemetry data.</returns>
        public static InvokeAgentData Build(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = BuildAttributes(
                invokeAgentDetails,
                tenantDetails,
                conversationId,
                request,
                callerAgentDetails,
                callerDetails,
                inputMessages,
                outputMessages,
                extraAttributes);

            return new InvokeAgentData(
                attributes,
                startTime,
                endTime,
                spanId,
                parentSpanId);
        }

        /// <summary>
        /// Builds all attributes for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the attributes.</param>
        /// <param name="outputMessages">Optional output messages to include in the attributes.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <returns>A dictionary of attribute key-value pairs.</returns>
        private static Dictionary<string, object?> BuildAttributes(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Add base agent details
            AddAgentDetails(attributes, invokeAgentDetails.Details);

            // Add tenant details
            AddTenantDetails(attributes, tenantDetails);

            // Add endpoint details
            AddEndpointDetails(attributes, invokeAgentDetails.Endpoint);

            // Add session ID
            AddIfNotNull(attributes, OpenTelemetryConstants.SessionIdKey, invokeAgentDetails.SessionId);

            // Add request details
            AddRequestDetails(attributes, request);

            // Add conversation ID
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Add caller details
            AddCallerDetails(attributes, callerDetails);

            // Add caller agent details
            AddCallerAgentDetails(attributes, callerAgentDetails);

            // Add input messages
            AddInputMessagesAttributes(attributes, inputMessages);

            // Add output messages
            AddOutputMessagesAttributes(attributes, outputMessages);

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }
    }
}
