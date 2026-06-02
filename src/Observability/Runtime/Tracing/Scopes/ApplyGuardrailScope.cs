// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Provides OpenTelemetry tracing scope for security guardrail evaluation operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Describes a security guardian evaluation. Multiple guardian spans MAY exist under a single
    /// operation span if multiple guardians are chained.
    /// </para>
    /// <para>
    /// Guardian spans SHOULD be children of the operation span they are protecting
    /// (e.g., inference or execute_tool spans).
    /// </para>
    /// </remarks>
    public sealed class ApplyGuardrailScope : OpenTelemetryScope
    {

        /// <summary>
        /// Creates and starts a new scope for guardrail evaluation tracing.
        /// </summary>
        /// <param name="details">Details of the guardrail evaluation (target, decision, guardian info, policy).</param>
        /// <param name="agentDetails">Information about the agent being guarded.</param>
        /// <param name="request">Optional request details for conversation context.</param>
        /// <param name="userDetails">Optional human user details.</param>
        /// <param name="spanDetails">Optional span configuration (parent context, timing, kind, span links).</param>
        /// <returns>A new ApplyGuardrailScope instance.</returns>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements:
        /// <list type="bullet">
        ///   <item><paramref name="details"/></item>
        ///   <item><paramref name="agentDetails"/></item>
        /// </list>
        /// </para>
        /// </remarks>
        public static ApplyGuardrailScope Start(
            GuardrailDetails details,
            AgentDetails agentDetails,
            Request? request = null,
            UserDetails? userDetails = null,
            SpanDetails? spanDetails = null) => new ApplyGuardrailScope(details, agentDetails, request, userDetails, spanDetails);

        private ApplyGuardrailScope(
            GuardrailDetails details,
            AgentDetails agentDetails,
            Request? request,
            UserDetails? userDetails,
            SpanDetails? spanDetails)
            : base(
                operationName: OpenTelemetryConstants.ApplyGuardrailOperationName,
                activityName: BuildActivityName(details),
                agentDetails: agentDetails,
                spanDetails: new SpanDetails(spanDetails?.SpanKind ?? ActivityKind.Internal, spanDetails?.ParentContext, spanDetails?.StartTime, spanDetails?.EndTime, spanDetails?.SpanLinks),
                userDetails: userDetails)
        {
            // Required attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityDecisionTypeKey, details.DecisionType.ToString().ToLowerInvariant());
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityTargetTypeKey, details.TargetType);

            // Guardian attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiGuardianIdKey, details.GuardianId);
            SetTagMaybe(OpenTelemetryConstants.GenAiGuardianNameKey, details.GuardianName);
            SetTagMaybe(OpenTelemetryConstants.GenAiGuardianProviderNameKey, details.GuardianProviderName);
            SetTagMaybe(OpenTelemetryConstants.GenAiGuardianVersionKey, details.GuardianVersion);

            // Target attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityTargetIdKey, details.TargetId);

            // Decision attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityDecisionReasonKey, details.DecisionReason);
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityDecisionCodeKey, details.DecisionCode);

            // Policy attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityPolicyIdKey, details.PolicyId);
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityPolicyNameKey, details.PolicyName);
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityPolicyVersionKey, details.PolicyVersion);

            // Content attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityContentInputHashKey, details.ContentInputHash);
            if (details.ContentModified.HasValue)
            {
                SetTagMaybe(OpenTelemetryConstants.GenAiSecurityContentModifiedKey, details.ContentModified.Value);
            }

            // Correlation attributes
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityExternalEventIdKey, details.ExternalEventId);

            // Request context
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityContentInputValueKey, request?.Content);
            SetTagMaybe(OpenTelemetryConstants.GenAiConversationIdKey, request?.ConversationId);
            if (request?.Channel != null)
            {
                SetTagMaybe(OpenTelemetryConstants.ChannelNameKey, request.Channel.Name);
                SetTagMaybe(OpenTelemetryConstants.ChannelLinkKey, request.Channel.Link);
            }
        }

        /// <summary>
        /// Records an updated decision on the guardrail span.
        /// Use this when the guardrail decision is determined after span creation.
        /// </summary>
        /// <param name="decisionType">The decision type made by the guardian.</param>
        /// <param name="reason">Optional human-readable explanation for the decision.</param>
        public void RecordDecision(GuardrailDecisionType decisionType, string? reason = null)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityDecisionTypeKey, decisionType.ToString().ToLowerInvariant());
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityDecisionReasonKey, reason);
        }

        /// <summary>
        /// Records the output content value for the guardrail evaluation (opt-in).
        /// </summary>
        /// <param name="outputValue">The output content after guardrail processing.</param>
        public void RecordContentOutput(string outputValue)
        {
            SetTagMaybe(OpenTelemetryConstants.GenAiSecurityContentOutputValueKey, outputValue);
        }

        /// <summary>
        /// Records a security finding event on the current span.
        /// Multiple findings may be recorded per guardrail evaluation.
        /// </summary>
        /// <param name="finding">The security finding to record.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="finding"/> is null.</exception>
        public void RecordFinding(GuardrailFinding finding)
        {
            if (finding == null)
            {
                throw new ArgumentNullException(nameof(finding));
            }

            var tags = new ActivityTagsCollection
            {
                { OpenTelemetryConstants.GenAiSecurityRiskCategoryKey, finding.RiskCategory },
                { OpenTelemetryConstants.GenAiSecurityRiskSeverityKey, finding.RiskSeverity }
            };

            if (finding.PolicyDecisionType != null)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityPolicyDecisionTypeKey, finding.PolicyDecisionType);
            }

            if (finding.PolicyId != null)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityPolicyIdKey, finding.PolicyId);
            }

            if (finding.PolicyName != null)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityPolicyNameKey, finding.PolicyName);
            }

            if (finding.PolicyVersion != null)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityPolicyVersionKey, finding.PolicyVersion);
            }

            if (finding.RiskScore.HasValue)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityRiskScoreKey, finding.RiskScore.Value);
            }

            if (finding.RiskMetadata != null)
            {
                tags.Add(OpenTelemetryConstants.GenAiSecurityRiskMetadataKey, finding.RiskMetadata);
            }

            var activityEvent = new ActivityEvent(OpenTelemetryConstants.GenAiSecurityFindingEventName, tags: tags);
            AddEvent(activityEvent);
        }

        private static string BuildActivityName(GuardrailDetails details)
        {
            if (!string.IsNullOrWhiteSpace(details.GuardianName))
            {
                return $"{OpenTelemetryConstants.ApplyGuardrailOperationName} {details.GuardianName} {details.TargetType}";
            }

            return $"{OpenTelemetryConstants.ApplyGuardrailOperationName} {details.TargetType}";
        }

    }
}
