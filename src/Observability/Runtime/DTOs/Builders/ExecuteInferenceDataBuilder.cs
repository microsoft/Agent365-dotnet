// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using System;
using System.Collections.Generic;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an ExecuteInferenceData instance.
    /// </summary>
    public class ExecuteInferenceDataBuilder : BaseDataBuilder<ExecuteInferenceData>
    {
        /// <summary>
        /// Builds complete data for an inference operation.
        /// </summary>
        /// <param name="inferenceCallDetails">The details of the inference call.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="inputMessages">Optional input messages for the inference.</param>
        /// <param name="outputMessages">Optional output messages from the inference.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="channel">Optional channel information for the inference call.</param>
        /// <param name="thoughtProcess">Optional agent thought process for the inference.</param>
        /// <param name="callerDetails">Optional details about the caller.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <returns>An ExecuteInferenceData object containing all telemetry data.</returns>
        public static ExecuteInferenceData Build(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            Channel? channel = null,
            string? thoughtProcess = null,
            CallerDetails? callerDetails = null,
            IDictionary<string, object?>? extraAttributes = null,
            string? traceId = null)
        {
            var attributes = BuildAttributes(
                inferenceCallDetails,
                agentDetails,
                conversationId,
                inputMessages,
                outputMessages,
                channel,
                thoughtProcess,
                callerDetails,
                extraAttributes);

            return new ExecuteInferenceData(attributes, startTime, endTime, spanId, parentSpanId, traceId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            string conversationId,
            string[]? inputMessages,
            string[]? outputMessages,
            Channel? channel,
            string? thoughtProcess,
            CallerDetails? callerDetails,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Agent details (includes tenant ID)
            AddAgentDetails(attributes, agentDetails);

            // Inference call details
            AddInferenceCallDetails(attributes, inferenceCallDetails);

            // Conversation
            AddIfNotNull(attributes, GenAiConversationIdKey, conversationId);

            // Input/output messages
            AddInputMessagesAttributes(attributes, inputMessages);
            AddOutputMessagesAttributes(attributes, outputMessages);

            // Thought process
            AddIfNotNull(attributes, GenAiAgentThoughtProcessKey, thoughtProcess);

            // Channel
            AddChannelAttributes(attributes, channel);

            // Add caller details
            AddCallerDetails(attributes, callerDetails);

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }

        private static void AddInferenceCallDetails(
            IDictionary<string, object?> attributes,
            InferenceCallDetails inferenceCallDetails)
        {
            AddIfNotNull(attributes, GenAiOperationNameKey, inferenceCallDetails.OperationName.ToString().ToLowerInvariant());
            AddIfNotNull(attributes, GenAiRequestModelKey, inferenceCallDetails.Model);
            AddIfNotNull(attributes, GenAiProviderNameKey, inferenceCallDetails.ProviderName);
            AddIfNotNull(attributes, GenAiUsageInputTokensKey, inferenceCallDetails.InputTokens?.ToString());
            AddIfNotNull(attributes, GenAiUsageOutputTokensKey, inferenceCallDetails.OutputTokens?.ToString());
            AddIfNotNull(attributes, GenAiResponseFinishReasonsKey, inferenceCallDetails.FinishReasons != null ? string.Join(",", inferenceCallDetails.FinishReasons) : null);
        }
    }
}
