// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for generative AI inference operations.
    /// </summary>
    public sealed class InferenceScope : OpenTelemetryScope
    {
        /// <summary>
        /// Creates and starts a new scope for inference tracing.
        /// </summary>
        public static InferenceScope Start(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null, string? conversationId = null, SourceMetadata? sourceMetadata = null) => new InferenceScope(details, agentDetails, tenantDetails, parentId, conversationId, sourceMetadata);

        private InferenceScope(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null, string? conversationId = null, SourceMetadata? sourceMetadata = null)
            : base(
                kind: ActivityKind.Client,
                agentDetails: agentDetails,
                tenantDetails: tenantDetails,
                operationName: details.OperationName.ToString(),
                activityName: $"{details.OperationName} {details.Model}",
                parentId: parentId,
                conversationId: conversationId,
                sourceMetadata: sourceMetadata)
        {
            SetTagMaybe(GenAiOperationNameKey, details.OperationName.ToString());
            SetTagMaybe(GenAiRequestModelKey, details.Model);
            SetTagMaybe(GenAiProviderNameKey, details.ProviderName);
            SetTagMaybe(GenAiUsageInputTokensKey, details.InputTokens?.ToString());
            SetTagMaybe(GenAiUsageOutputTokensKey, details.OutputTokens?.ToString());
            SetTagMaybe(GenAiResponseFinishReasonsKey, details.FinishReasons != null ? string.Join(",", details.FinishReasons) : null);
            SetTagMaybe(GenAiResponseIdKey, details.ResponseId);
        }

        /// <summary>
        /// Records the input messages for telemetry tracking.
        /// </summary>
        public void RecordInputMessages(string[] messages)
        {
            SetTagMaybe(GenAiInputMessagesKey, string.Join(",", messages));
        }

        /// <summary>
        /// Records the output messages for telemetry tracking.
        /// </summary>
        public void RecordOutputMessages(string[] messages)
        {
            SetTagMaybe(GenAiOutputMessagesKey, string.Join(",", messages));
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
        /// Records the response id for telemetry tracking.
        /// </summary>
        public void RecordResponseId(string responseId)
        {
            if (!string.IsNullOrEmpty(responseId))
            {
                SetTagMaybe(GenAiResponseIdKey, responseId);
            }
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
    }
}