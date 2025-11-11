// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
        public static InferenceScope Start(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null) => new InferenceScope(details, agentDetails, tenantDetails, parentId);

        private InferenceScope(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null)
            : base(
                ActivityKind.Client,
                agentDetails,
                tenantDetails,
                GetOperationNameValue(details.OperationName),
                $"{GetOperationNameValue(details.OperationName)} {details.Model}",
                parentId: parentId)
        {
            SetTagMaybe(GenAiOperationNameKey, GetOperationNameValue(details.OperationName));
            SetTagMaybe(GenAiRequestModelKey, details.Model);
            SetTagMaybe(GenAiProviderNameKey, details.ProviderName);
            SetTagMaybe(GenAiUsageInputTokensKey, details.InputTokens?.ToString());
            SetTagMaybe(GenAiUsageOutputTokensKey, details.OutputTokens?.ToString());
            SetTagMaybe(GenAiResponseFinishReasonsKey, details.FinishReasons != null ? string.Join(",", details.FinishReasons) : null);
            SetTagMaybe(GenAiResponseIdKey, details.ResponseId);
        }

        /// <summary>
        /// Gets the string value for an InferenceOperationType enum member.
        /// </summary>
        private static string GetOperationNameValue(InferenceOperationType operationType)
        {
            var memberInfo = typeof(InferenceOperationType).GetMember(operationType.ToString()).FirstOrDefault();
            var enumMemberAttribute = memberInfo?.GetCustomAttribute<EnumMemberAttribute>();
            return enumMemberAttribute?.Value ?? operationType.ToString();
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