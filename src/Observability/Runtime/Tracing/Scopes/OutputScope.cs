// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;

using Microsoft.Agents.A365.Observability.Runtime.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent output operations.
    /// </summary>
    /// <remarks>
    /// Output messages are set once (via the constructor or <see cref="RecordOutputMessages(IEnumerable{string})"/>)
    /// rather than accumulated. For streaming scenarios, the agent developer should collect all output
    /// and pass the final result to <see cref="OutputScope"/>.
    /// </remarks>
    public sealed class OutputScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for output tracing.
        /// </summary>
        public const string OperationName = "output_messages";

        /// <summary>
        /// Creates and starts a new scope for output tracing.
        /// </summary>
        /// <param name="request">Request details for the output operation.</param>
        /// <param name="response">Response containing output messages.</param>
        /// <param name="agentDetails">Information about the agent producing the output.</param>
        /// <param name="userDetails">Optional human user details.</param>
        /// <param name="spanDetails">Optional span configuration (parent context, timing, span links).</param>
        /// <returns>A new OutputScope instance.</returns>
        public static OutputScope Start(Request request, Response response, AgentDetails agentDetails, UserDetails? userDetails = null, SpanDetails? spanDetails = null)
            => new OutputScope(request, response, agentDetails, userDetails, spanDetails);

        private OutputScope(Request request, Response response, AgentDetails agentDetails, UserDetails? userDetails, SpanDetails? spanDetails)
            : base(
                operationName: OperationName,
                activityName: $"{OperationName} {agentDetails.AgentId}",
                agentDetails: agentDetails,
                spanDetails: spanDetails ?? new SpanDetails(ActivityKind.Client),
                userDetails: userDetails)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, request?.ConversationId);

            if (request?.Channel != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ChannelNameKey, request.Channel.Name);
                SetTagMaybe(OpenTelemetryConstants.ChannelLinkKey, request.Channel.Link);
            }

            if (response?.ToolResultObject != null)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(response.ToolResultObject));
            }
            else if (response?.OutputContent != null)
            {
                SetOutput(response.OutputContent);
            }
            else if (response?.Messages != null && response.Messages.Count > 0)
            {
                SetOutput(response.Messages);
            }
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// Overwrites any previously set output messages.
        /// Plain strings are auto-wrapped as OTEL OutputMessage with role "assistant".
        /// </summary>
        /// <param name="messages">The messages to set as output.</param>
        public void RecordOutputMessages(IEnumerable<string> messages)
        {
            if (messages == null)
            {
                return;
            }

            SetOutput(messages);
        }

        /// <summary>
        /// Records structured output messages for telemetry tracking.
        /// Overwrites any previously set output messages.
        /// </summary>
        /// <param name="messages">The versioned output messages wrapper.</param>
        public void RecordOutputMessages(OutputMessages messages)
        {
            if (messages == null)
            {
                return;
            }

            SetOutput(messages);
        }

        /// <summary>
        /// Records a tool call result dictionary for telemetry tracking.
        /// Per OTEL spec, tool call results are expected to be objects and are serialized to JSON.
        /// Overwrites any previously set output messages.
        /// </summary>
        /// <param name="toolResult">The tool call result as a structured dictionary.</param>
        public void RecordOutputMessages(IDictionary<string, object> toolResult)
        {
            if (toolResult == null)
            {
                return;
            }

            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(toolResult));
        }

        private void SetOutput(OutputMessages messages)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(messages));
        }

        private void SetOutput(IEnumerable<string> messages)
        {
            var normalized = MessageUtils.NormalizeOutputMessages(messages);
            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, MessageUtils.Serialize(normalized));
        }
    }
}