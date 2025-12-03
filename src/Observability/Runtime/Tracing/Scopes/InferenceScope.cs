// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

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
        /// <param name="details">The details of the inference call.</param>
        /// <param name="agentDetails">The details of the agent performing the inference.</param>
        /// <param name="tenantDetails">The tenant details for the inference operation.</param>
        /// <param name="parentId">Optional parent activity ID.</param>
        /// <returns>A new InferenceScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set with appropriate values for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="details"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        ///   <item><paramref name="tenantDetails"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// </remarks>
        public static InferenceScope Start(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null) => new InferenceScope(details, agentDetails, tenantDetails, parentId);

        private InferenceScope(InferenceCallDetails details, AgentDetails agentDetails, TenantDetails tenantDetails, string? parentId = null)
            : base(
                ActivityKind.Client,
                agentDetails,
                tenantDetails,
                details.OperationName.ToString(),
                $"{details.OperationName} {details.Model}",
                parentId: parentId)
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