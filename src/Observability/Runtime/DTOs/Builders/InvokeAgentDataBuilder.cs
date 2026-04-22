// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an InvokeAgentData instance
    /// </summary>
    public class InvokeAgentDataBuilder : BaseDataBuilder<InvokeAgentData>
    {
        private const string InvokeAgentOperationName = "invoke_agent";

        /// <summary>
        /// Builds complete data for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentScopeDetails">The scope-level details of the agent invocation.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The required conversation ID for the agent invocation.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerDetails">The details of the caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the telemetry.</param>
        /// <param name="outputMessages">Optional output messages to include in the telemetry.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <param name="spanKind">Optional span kind override. Use <see cref="SpanKindConstants.Client"/> or <see cref="SpanKindConstants.Server"/> as appropriate.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <returns>An InvokeAgentData object containing all telemetry data.</returns>
        public static InvokeAgentData Build(
            InvokeAgentScopeDetails invokeAgentScopeDetails,
            AgentDetails agentDetails,
            string conversationId,
            Request? request = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            IDictionary<string, object?>? extraAttributes = null,
            string? spanKind = null,
            string? traceId = null)
        {
            var attributes = BuildAttributes(
                invokeAgentScopeDetails,
                agentDetails,
                conversationId,
                request,
                callerDetails,
                inputMessages,
                outputMessages,
                extraAttributes);

            return new InvokeAgentData(
                attributes,
                startTime,
                endTime,
                spanId,
                parentSpanId,
                spanKind,
                traceId);
        }

        /// <summary>
        /// Builds all attributes for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentScopeDetails">The scope-level details of the agent invocation.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerDetails">The details of the caller.</param>
        /// <param name="inputMessages">Optional input messages to include in the attributes.</param>
        /// <param name="outputMessages">Optional output messages to include in the attributes.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <returns>A dictionary of attribute key-value pairs.</returns>
        private static Dictionary<string, object?> BuildAttributes(
            InvokeAgentScopeDetails invokeAgentScopeDetails,
            AgentDetails agentDetails,
            string conversationId,
            Request? request = null,
            CallerDetails? callerDetails = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Operation name
            AddIfNotNull(attributes, GenAiOperationNameKey, InvokeAgentDataBuilder.InvokeAgentOperationName);

            // Add agent details (includes tenant ID)
            AddAgentDetails(attributes, agentDetails);

            // Add endpoint details
            AddEndpointDetails(attributes, invokeAgentScopeDetails.Endpoint);

            // Add request details
            AddRequestDetails(attributes, request);

            // Add conversation ID
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Add caller details (includes user details and caller agent details)
            AddCallerDetails(attributes, callerDetails);

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
