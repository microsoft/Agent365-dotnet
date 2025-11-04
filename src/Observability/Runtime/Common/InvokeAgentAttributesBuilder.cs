// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Builds attributes for invoke_agent operations that can be used for both tracing and logging.
    /// </summary>
    public static class InvokeAgentAttributesBuilder
    {
        /// <summary>
        /// Builds all attributes for an invoke_agent operation.
        /// </summary>
        /// <param name="invokeAgentDetails">The details of the agent invocation.</param>
        /// <param name="tenantDetails">The tenant details.</param>
        /// <param name="request">The request content for the invoked agent.</param>
        /// <param name="callerAgentDetails">The details of the caller agent.</param>
        /// <param name="callerDetails">The details of the non-agentic caller.</param>
        /// <param name="conversationId">The conversation ID for the agent invocation.</param>
        /// <returns>A dictionary of attribute key-value pairs.</returns>
        public static Dictionary<string, object?> BuildAttributes(
            InvokeAgentDetails invokeAgentDetails,
            TenantDetails tenantDetails,
            Request? request = null,
            AgentDetails? callerAgentDetails = null,
            CallerDetails? callerDetails = null,
            string? conversationId = null)
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
            if (request != null)
            {
                AddRequestDetails(attributes, request);
            }

            // Add conversation ID
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Add caller details
            if (callerDetails != null)
            {
                AddCallerDetails(attributes, callerDetails);
            }

            // Add caller agent details
            if (callerAgentDetails != null)
            {
                AddCallerAgentDetails(attributes, callerAgentDetails);
            }

            return attributes;
        }

        /// <summary>
        /// Adds agent details to the attributes dictionary.
        /// </summary>
        private static void AddAgentDetails(Dictionary<string, object?> attributes, AgentDetails agentDetails)
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
        private static void AddTenantDetails(Dictionary<string, object?> attributes, TenantDetails tenantDetails)
        {
            if (tenantDetails == null) return;

            AddIfNotNull(attributes, OpenTelemetryConstants.TenantIdKey, tenantDetails.TenantId);
        }

        /// <summary>
        /// Adds endpoint details to the attributes dictionary.
        /// </summary>
        private static void AddEndpointDetails(Dictionary<string, object?> attributes, Uri endpoint)
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
        private static void AddRequestDetails(Dictionary<string, object?> attributes, Request request)
        {
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceIdKey, request.SourceMetadata?.Id);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceNameKey, request.SourceMetadata?.Name);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionSourceDescriptionKey, request.SourceMetadata?.Description);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiExecutionTypeKey, request.ExecutionType?.ToString());
        }

        /// <summary>
        /// Adds caller details to the attributes dictionary.
        /// </summary>
        private static void AddCallerDetails(Dictionary<string, object?> attributes, CallerDetails callerDetails)
        {
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerUserIdKey, callerDetails.CallerUserId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId);
        }

        /// <summary>
        /// Adds caller agent details to the attributes dictionary.
        /// </summary>
        private static void AddCallerAgentDetails(Dictionary<string, object?> attributes, AgentDetails callerAgentDetails)
        {
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
        private static void AddIfNotNull(Dictionary<string, object?> attributes, string key, object? value)
        {
            if (value != null)
            {
                attributes[key] = value;
            }
        }
    }
}
