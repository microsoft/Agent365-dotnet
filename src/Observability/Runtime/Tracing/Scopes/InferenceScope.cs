// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

using Microsoft.Agents.A365.Observability.Runtime.Tracing;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for generative AI inference operations.
    /// </summary>
    /// <remarks>
    /// <see href="https://learn.microsoft.com/microsoft-agent-365/developer/observability?tabs=dotnet#inference">Learn more about inference</see>
    /// </remarks>
    public sealed class InferenceScope : OpenTelemetryScope
    {
        /// <summary>
        /// Creates and starts a new scope for inference tracing.
        /// </summary>
        /// <param name="request">Request details for the inference.</param>
        /// <param name="details">Details of the inference call (operation name, model, provider, token usage, finish reasons, response ID).</param>
        /// <param name="agentDetails">Information about the agent executing the inference (service, version, identifiers).</param>
        /// <param name="userDetails">Optional human user details.</param>
        /// <param name="spanDetails">Optional span configuration (parent context, timing, span links).</param>
        /// <returns>A new InferenceScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="details"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static InferenceScope Start(Request request, InferenceCallDetails details, AgentDetails agentDetails, UserDetails? userDetails = null, SpanDetails? spanDetails = null) => new InferenceScope(request, details, agentDetails, userDetails, spanDetails);

        private InferenceScope(Request request, InferenceCallDetails details, AgentDetails agentDetails, UserDetails? userDetails, SpanDetails? spanDetails)
            : base(
                operationName: details.OperationName.ToString(),
                activityName: $"{details.OperationName} {details.Model}",
                agentDetails: agentDetails,
                spanDetails: spanDetails ?? new SpanDetails(ActivityKind.Client),
                userDetails: userDetails)
        {
            SetTagMaybe(GenAiOperationNameKey, details.OperationName.ToString());
            SetTagMaybe(GenAiRequestModelKey, details.Model);
            SetTagMaybe(GenAiProviderNameKey, details.ProviderName);
            SetTagMaybe(GenAiUsageInputTokensKey, details.InputTokens?.ToString());
            SetTagMaybe(GenAiUsageOutputTokensKey, details.OutputTokens?.ToString());
            SetTagMaybe(GenAiResponseFinishReasonsKey, details.FinishReasons != null ? string.Join(",", details.FinishReasons) : null);
            SetTagMaybe(GenAiConversationIdKey, request?.ConversationId);

            if (request?.InputContent != null)
            {
                RecordInputMessages(request.InputContent);
            }
            else if (request?.Content != null)
            {
                RecordInputMessages(new[] { request.Content });
            }

            if (request?.Channel != null)
            {
                SetTagMaybe(ChannelNameKey, request.Channel.Name);
                SetTagMaybe(ChannelLinkKey, request.Channel.Link);
            }
        }

        /// <summary>
        /// Records the input messages for telemetry tracking.
        /// Plain strings are auto-wrapped as OTEL ChatMessage with role "user".
        /// </summary>
        public void RecordInputMessages(string[] messages)
        {
            var wrapper = MessageUtils.NormalizeInputMessages(messages);
            SetTagMaybe(GenAiInputMessagesKey, MessageUtils.Serialize(wrapper));
        }

        /// <summary>
        /// Records structured input messages for telemetry tracking.
        /// </summary>
        /// <param name="messages">The versioned input messages wrapper.</param>
        public void RecordInputMessages(InputMessages messages)
        {
            SetTagMaybe(GenAiInputMessagesKey, MessageUtils.Serialize(messages));
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// Plain strings are auto-wrapped as OTEL OutputMessage with role "assistant".
        /// </summary>
        public void RecordOutputMessages(string[] messages)
        {
            var wrapper = MessageUtils.NormalizeOutputMessages(messages);
            SetTagMaybe(GenAiOutputMessagesKey, MessageUtils.Serialize(wrapper));
        }

        /// <summary>
        /// Records structured output messages for telemetry tracking.
        /// </summary>
        /// <param name="messages">The versioned output messages wrapper.</param>
        public void RecordOutputMessages(OutputMessages messages)
        {
            SetTagMaybe(GenAiOutputMessagesKey, MessageUtils.Serialize(messages));
        }

        /// <summary>
        /// Records the number of input tokens for telemetry tracking.
        /// </summary>
        public void RecordInputTokens(int inputTokens)
        {
            SetTagMaybe(GenAiUsageInputTokensKey, inputTokens.ToString());
        }

        /// <summary>
        /// Records the number of output tokens for telemetry tracking.
        /// </summary>
        public void RecordOutputTokens(int outputTokens)
        {
            SetTagMaybe(GenAiUsageOutputTokensKey, outputTokens.ToString());
        }

        /// <summary>
        /// Records the finish reasons for telemetry tracking.
        /// </summary>
        public void RecordFinishReasons(string[] finishReasons)
        {
            if (finishReasons != null)
            {
                SetTagMaybe(GenAiResponseFinishReasonsKey, string.Join(",", finishReasons));
            }
        }

        /// <summary>
        /// Records the agent's thought process for telemetry tracking.
        /// </summary>
        public void RecordThoughtProcess(string thoughtProcess)
        {
            if (!string.IsNullOrEmpty(thoughtProcess))
            {
                SetTagMaybe(GenAiAgentThoughtProcessKey, thoughtProcess);
            }
        }
    }
}