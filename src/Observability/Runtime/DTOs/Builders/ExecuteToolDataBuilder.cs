// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an ExecuteToolData instance.
    /// </summary>
    public class ExecuteToolDataBuilder : BaseDataBuilder<ExecuteToolData>
    {
        private const string ExecuteToolOperationName = "execute_tool";

        /// <summary>
        /// Builds complete data for an execute_tool operation.
        /// </summary>
        /// <param name="toolCallDetails">The details of the tool call.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="responseContent">Optional response content from the tool.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="channel">Optional channel information for the operation.</param>
        /// <param name="callerDetails">Optional details about the caller.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <param name="spanKind">Optional span kind override. Use <see cref="SpanKindConstants.Internal"/> or <see cref="SpanKindConstants.Client"/> as appropriate.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <returns>An ExecuteToolData object containing all telemetry data.</returns>
        public static ExecuteToolData Build(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string? responseContent = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            IDictionary<string, object?>? extraAttributes = null,
            string? spanKind = null,
            string? traceId = null)
        {
            var attributes = BuildAttributes(toolCallDetails, agentDetails, conversationId, responseContent, channel, callerDetails, extraAttributes);

            return new ExecuteToolData(attributes, startTime, endTime, spanId, parentSpanId, spanKind, traceId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            ToolCallDetails toolCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string? responseContent,
            Channel? channel,
            CallerDetails? callerDetails,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Operation name
            AddIfNotNull(attributes, GenAiOperationNameKey, ExecuteToolDataBuilder.ExecuteToolOperationName);

            AddAgentDetails(attributes, agentDetails);

            // Tool details
            AddToolDetails(attributes, toolCallDetails);

            // Conversation
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Response content — ensure JSON object per OTEL spec
            if (responseContent != null)
            {
                if (MessageUtils.IsJson(responseContent))
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolCallResultKey, responseContent);
                }
                else
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolCallResultKey,
                        MessageUtils.Serialize(new Dictionary<string, object> { { "result", responseContent } }));
                }
            }

            // Channel
            AddChannelAttributes(attributes, channel);

            // Add caller details
            AddCallerDetails(attributes, callerDetails);

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }

        private static void AddToolDetails(
            Dictionary<string, object?> attributes,
            ToolCallDetails toolCallDetails)
        {
            var (toolName, arguments, toolCallId, description, toolType, endpoint, toolServerName) = toolCallDetails;
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolNameKey, toolName);

            // Arguments — prefer structured dict, then ensure JSON string per OTEL spec
            if (toolCallDetails.ArgumentsObject != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolArgumentsKey, MessageUtils.Serialize(toolCallDetails.ArgumentsObject));
            }
            else if (arguments != null)
            {
                if (MessageUtils.IsJson(arguments))
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolArgumentsKey, arguments);
                }
                else
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolArgumentsKey,
                        MessageUtils.Serialize(new Dictionary<string, object> { { "arguments", arguments } }));
                }
            }

            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolCallIdKey, toolCallId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolDescriptionKey, description);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolTypeKey, toolType);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiToolServerNameKey, toolServerName);
            if (endpoint != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.ServerAddressKey, endpoint.Host);
                if (endpoint.Port != 443)
                {
                    AddIfNotNull(attributes, OpenTelemetryConstants.ServerPortKey, endpoint.Port.ToString());
                }
            }
        }
    }
}
