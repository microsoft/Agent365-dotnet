// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an OutputData instance.
    /// </summary>
    public class OutputDataBuilder : BaseDataBuilder<OutputData>
    {
        private const string OutputMessagesOperationName = "output_messages";

        /// <summary>
        /// Builds complete data for an output_messages operation.
        /// </summary>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="response">The response containing output messages.</param>
        /// <param name="conversationId">Optional conversation ID for the output operation.</param>
        /// <param name="channel">Optional channel information for the output operation.</param>
        /// <param name="callerDetails">Optional details about the caller.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <returns>An OutputData object containing all telemetry data.</returns>
        public static OutputData Build(
            AgentDetails agentDetails,
            Response response,
            string? conversationId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            IDictionary<string, object?>? extraAttributes = null,
            string? traceId = null)
        {
            var attributes = BuildAttributes(agentDetails, response, conversationId, channel, callerDetails, extraAttributes);

            return new OutputData(attributes, startTime, endTime, spanId, parentSpanId, traceId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            AgentDetails agentDetails,
            Response response,
            string? conversationId,
            Channel? channel,
            CallerDetails? callerDetails,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Operation name
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOperationNameKey, OutputMessagesOperationName);
            
            AddAgentDetails(attributes, agentDetails);

            // Output messages from response — prefer structured content
            if (response.ToolResultObject != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(response.ToolResultObject));
            }
            else if (response.OutputContent != null)
            {
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(response.OutputContent));
            }
            else if (response.Messages.Count > 0)
            {
                var wrapper = MessageUtils.NormalizeOutputMessages(response.Messages);
                AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(wrapper));
            }

            // Conversation ID
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Channel
            AddChannelAttributes(attributes, channel);

            // Caller details
            AddCallerDetails(attributes, callerDetails);

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }
    }
}
