// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for AI agent output operations.
    /// </summary>
    public sealed class OutputScope : OpenTelemetryScope
    {
        /// <summary>
        /// The operation name for output tracing.
        /// </summary>
        public const string OperationName = "output_messages";

        private readonly List<string> outputMessages = new List<string>();

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

            if (response?.Messages != null && response.Messages.Count > 0)
            {
                foreach (var message in response.Messages)
                {
                    outputMessages.Add(message);
                }

                SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages));
            }
        }

        /// <summary>
        /// Records additional output messages and appends them to the existing output messages attribute.
        /// </summary>
        /// <param name="messages">The messages to append to the output.</param>
        public void RecordOutputMessages(IEnumerable<string> messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                outputMessages.Add(message);
            }

            SetTagMaybe(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", outputMessages));
        }
    }
}
