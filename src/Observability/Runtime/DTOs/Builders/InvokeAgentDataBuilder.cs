using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an InvokeAgentData instance
    /// </summary>
    public class InvokeAgentDataBuilder
    {
        /// <summary>
        /// Builds complete data for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <param name="inputMessages">Optional input messages to include in the telemetry.</param>
        /// <param name="outputMessages">Optional output messages to include in the telemetry.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <returns>An InvokeAgentData object containing all telemetry data.</returns>
        public static InvokeAgentData Build(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string? conversationId = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            var attributes = BuildAttributes(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
                inputMessages,
                outputMessages);

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
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <param name="inputMessages">Optional input messages to include in the attributes.</param>
        /// <param name="outputMessages">Optional output messages to include in the attributes.</param>
        /// <returns>A dictionary of attribute key-value pairs.</returns>
        private static Dictionary<string, object?> BuildAttributes(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string? conversationId = null,
            string[]? inputMessages = null,
            string[]? outputMessages = null)
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

            return attributes;
        }

        /// <summary>
        /// Adds attributes for input messages.
        /// </summary>
        private static void AddInputMessagesAttributes(IDictionary<string, object?> attributes, string[]? messages)
        {
            if (messages != null && messages.Length > 0)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiInputMessagesKey, string.Join(",", messages));
            }
        }

        /// <summary>
        /// Adds attributes for output messages.
        /// </summary>
        private static void AddOutputMessagesAttributes(IDictionary<string, object?> attributes, string[]? messages)
        {
            if (messages != null && messages.Length > 0)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", messages));
            }
        }

        /// <summary>
        /// Adds agent details to the attributes dictionary.
        /// </summary>
        private static void AddAgentDetails(IDictionary<string, object?> attributes, AgentDetails agentDetails)
        {
            if (agentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentDescriptionKey, agentDetails.AgentDescription);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentAUIDKey, agentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentUPNKey, agentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiAgentBlueprintIdKey, agentDetails.AgentBlueprintId);
        }

        /// <summary>
        /// Adds tenant details to the attributes dictionary.
        /// </summary>
        private static void AddTenantDetails(IDictionary<string, object?> attributes, TenantDetails tenantDetails)
        {
            if (tenantDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId);
        }

        /// <summary>
        /// Adds endpoint details to the attributes dictionary.
        /// </summary>
        private static void AddEndpointDetails(IDictionary<string, object?> attributes, Uri endpoint)
        {
            if (endpoint == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host);

            // Only record port if it is different from 443
            if (endpoint.Port != 443)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port);
            }
        }

        /// <summary>
        /// Adds request details to the attributes dictionary.
        /// </summary>
        private static void AddRequestDetails(IDictionary<string, object?> attributes, Request? request)
        {
            if (request == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceIdKey, request.SourceMetadata?.Id);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceNameKey, request.SourceMetadata?.Name);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceDescriptionKey, request.SourceMetadata?.Description);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionTypeKey, request.ExecutionType?.ToString());
        }

        /// <summary>
        /// Adds caller details to the attributes dictionary.
        /// </summary>
        private static void AddCallerDetails(IDictionary<string, object?> attributes, CallerDetails? callerDetails)
        {
            if (callerDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerUserIdKey, callerDetails.CallerUserId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
        }

        /// <summary>
        /// Adds caller agent details to the attributes dictionary.
        /// </summary>
        private static void AddCallerAgentDetails(IDictionary<string, object?> attributes, AgentDetails? callerAgentDetails)
        {
            if (callerAgentDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentNameKey, callerAgentDetails.AgentName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentIdKey, callerAgentDetails.AgentId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentApplicationIdKey, callerAgentDetails.AgentBlueprintId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentAUIDKey, callerAgentDetails.AgentAUID);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentUPNKey, callerAgentDetails.AgentUPN);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerAgentTenantKey, callerAgentDetails.TenantId);
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the value is not null.
        /// </summary>
        private static void AddIfNotNull(IDictionary<string, object?> attributes, string key, object? value)
        {
            if (value != null)
            {
                attributes[key] = value;
            }
        }
    }
}
