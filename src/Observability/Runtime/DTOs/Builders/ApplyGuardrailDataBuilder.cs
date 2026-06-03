// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders
{
    /// <summary>
    /// Builds an ApplyGuardrailData instance.
    /// </summary>
    public class ApplyGuardrailDataBuilder : BaseDataBuilder<ApplyGuardrailData>
    {
        /// <summary>
        /// Builds complete data for an apply_guardrail operation.
        /// </summary>
        /// <param name="guardrailDetails">The details of the guardrail evaluation.</param>
        /// <param name="agentDetails">The details of the agent (includes tenant ID).</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="parentSpanId">The parent span ID for distributed tracing.</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation.</param>
        /// <param name="channel">Optional channel information for the operation.</param>
        /// <param name="callerDetails">Optional details about the caller.</param>
        /// <param name="extraAttributes">Optional dictionary of extra attributes.</param>
        /// <param name="spanKind">Optional span kind override.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        /// <returns>An ApplyGuardrailData object containing all telemetry data.</returns>
        public static ApplyGuardrailData Build(
            GuardrailDetails guardrailDetails,
            AgentDetails agentDetails,
            string conversationId,
            string parentSpanId,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            Channel? channel = null,
            CallerDetails? callerDetails = null,
            IDictionary<string, object?>? extraAttributes = null,
            string? spanKind = null,
            string? traceId = null)
        {
            var attributes = BuildAttributes(guardrailDetails, agentDetails, conversationId, channel, callerDetails, extraAttributes);

            return new ApplyGuardrailData(parentSpanId, attributes, startTime, endTime, spanId, spanKind, traceId);
        }

        private static Dictionary<string, object?> BuildAttributes(
            GuardrailDetails guardrailDetails,
            AgentDetails agentDetails,
            string conversationId,
            Channel? channel,
            CallerDetails? callerDetails,
            IDictionary<string, object?>? extraAttributes = null)
        {
            var attributes = new Dictionary<string, object?>();

            // SDK attributes
            AddSdkAttributes(attributes);

            // Operation name
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiOperationNameKey, OpenTelemetryConstants.ApplyGuardrailOperationName);

            AddAgentDetails(attributes, agentDetails);

            // Guardrail details
            AddGuardrailDetails(attributes, guardrailDetails);

            // Conversation
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

            // Channel
            AddChannelAttributes(attributes, channel);

            // Caller details
            AddCallerDetails(attributes, callerDetails);

            // Extra attributes
            AddExtraAttributes(attributes, extraAttributes);

            return attributes;
        }

        private static void AddGuardrailDetails(
            Dictionary<string, object?> attributes,
            GuardrailDetails details)
        {
            // Required attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityDecisionTypeKey, details.DecisionType.ToString().ToLowerInvariant());
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityTargetTypeKey, details.TargetType);

            // Guardian attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiGuardianIdKey, details.GuardianId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiGuardianNameKey, details.GuardianName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiGuardianProviderNameKey, details.GuardianProviderName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiGuardianVersionKey, details.GuardianVersion);

            // Target attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityTargetIdKey, details.TargetId);

            // Decision attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityDecisionReasonKey, details.DecisionReason);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityDecisionCodeKey, details.DecisionCode);

            // Policy attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityPolicyIdKey, details.PolicyId);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityPolicyNameKey, details.PolicyName);
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityPolicyVersionKey, details.PolicyVersion);

            // Content attributes
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityContentInputHashKey, details.ContentInputHash);
            if (details.ContentModified.HasValue)
            {
                attributes[OpenTelemetryConstants.GenAiSecurityContentModifiedKey] = details.ContentModified.Value;
            }

            // Correlation
            AddIfNotNull(attributes, OpenTelemetryConstants.GenAiSecurityExternalEventIdKey, details.ExternalEventId);
        }

    }
}
