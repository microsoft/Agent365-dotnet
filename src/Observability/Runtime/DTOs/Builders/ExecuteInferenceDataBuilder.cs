// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

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
        /// <param name="agentDetails">The details of the agent.</param>
        /// <param name="tenantDetails">The details of the tenant.</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="inputMessages">Optional input messages for the inference.</param>
        /// <param name="outputMessages">Optional output messages from the inference.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <returns>An ExecuteInferenceData object containing all telemetry data.</returns>
        public static ExecuteInferenceData Build(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string[]? inputMessages = null,
            string[]? outputMessages = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = BuildAttributes(
                inferenceCallDetails,
                agentDetails,
                tenantDetails,
                conversationId,
                inputMessages,
                outputMessages,
                extraAttributes);

            return new ExecuteInferenceData(attributes, startTime, endTime, spanId, parentSpanId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            InferenceCallDetails inferenceCallDetails,
            AgentDetails agentDetails,
            TenantDetails tenantDetails,
            string conversationId,
            string[]? inputMessages,
            string[]? outputMessages,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // Agent & tenant
            AddAgentDetails(attributes, agentDetails);
            AddTenantDetails(attributes, tenantDetails);

            // Inference call details
            AddInferenceCallDetails(attributes, inferenceCallDetails);

            // Conversation
            AddIfNotNull(attributes, GenAiConversationIdKey, conversationId);

            // Input/output messages
            AddInputMessagesAttributes(attributes, inputMessages);
            AddOutputMessagesAttributes(attributes, outputMessages);

            // Add any extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }

        private static void AddInferenceCallDetails(
            IDictionary<string, object?> attributes,
            InferenceCallDetails inferenceCallDetails)
        {
            var (operationName, model, providerName, inputTokens, outputTokens, finishReasons, responseId) = inferenceCallDetails;
            AddIfNotNull(attributes, GenAiOperationNameKey, operationName.ToString());
            AddIfNotNull(attributes, GenAiRequestModelKey, model);
            AddIfNotNull(attributes, GenAiProviderNameKey, providerName);
            AddIfNotNull(attributes, GenAiUsageInputTokensKey, inputTokens);
            AddIfNotNull(attributes, GenAiUsageOutputTokensKey, outputTokens);
            AddIfNotNull(attributes, GenAiResponseFinishReasonsKey, finishReasons != null ? string.Join(",", finishReasons) : null);
            AddIfNotNull(attributes, GenAiResponseIdKey, responseId);
        }
    }
}
