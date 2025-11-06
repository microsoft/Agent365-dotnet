// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an ExecuteToolData instance.
    /// </summary>
    public class ExecuteToolDataBuilder : BaseDataBuilder<ExecuteToolData>
    {
        /// <summary>
        /// Builds complete data for an execute_tool operation.
        /// </summary>
        /// <param name="toolCallDetails">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="responseContent">Optional response content from the tool.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <returns>An ExecuteToolData object containing all telemetry data.</returns>
        public static ExecuteToolData Build(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string? responseContent = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            var attributes = BuildAttributes(toolCallDetails, agentDetails, tenantDetails, conversationId, responseContent);

            return new ExecuteToolData(attributes, startTime, endTime, spanId, parentSpanId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string? responseContent)
        {
            var attributes = new Dictionary<string, object?>();

            // Agent & tenant
            AddAgentDetails(attributes, agentDetails);
            AddTenantDetails(attributes, tenantDetails);

            // Tool details
            AddToolDetails(attributes, toolCallDetails);

            // Conversation
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Response content if supplied
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiEventContent, responseContent);

            return attributes;
        }

        private static void AddToolDetails(
            Dictionary<string, object?> attributes,
            ToolCallDetails toolCallDetails)
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint) = toolCallDetails;
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolNameKey, toolName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolCallIdKey, toolCallId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolDescriptionKey, description);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolTypeKey, toolType);
            if (endpoint != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port);
                }
            }
        }
    }
}
