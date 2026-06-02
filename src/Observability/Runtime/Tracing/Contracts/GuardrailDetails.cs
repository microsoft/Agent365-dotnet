// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Details of a guardrail evaluation for security operations tracing.
    /// </summary>
    public sealed class GuardrailDetails : IEquatable<GuardrailDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GuardrailDetails"/> class.
        /// </summary>
        /// <param name="targetType">The type of content or action the guardrail is applied to (required). See <see cref="GuardrailTargetType"/> for well-known values.</param>
        /// <param name="decisionType">The decision made by the guardian (required).</param>
        /// <param name="guardianName">Human-readable name of the guardian.</param>
        /// <param name="guardianId">Unique identifier of the guardian.</param>
        /// <param name="guardianProviderName">Provider of the guardian service (e.g., azure.ai.content_safety).</param>
        /// <param name="guardianVersion">Version of the guardian.</param>
        /// <param name="targetId">Identifier of the target being guarded.</param>
        /// <param name="decisionReason">Human-readable explanation for the decision.</param>
        /// <param name="decisionCode">Machine-readable decision code.</param>
        /// <param name="policyId">Identifier of the policy that triggered the decision.</param>
        /// <param name="policyName">Human-readable name of the policy.</param>
        /// <param name="policyVersion">Version of the policy.</param>
        /// <param name="contentInputHash">Hash of the input content for forensic correlation.</param>
        /// <param name="contentModified">Whether content was modified by the guardrail.</param>
        /// <param name="externalEventId">External correlation identifier for SIEM systems.</param>
        public GuardrailDetails(
            string targetType,
            GuardrailDecisionType decisionType,
            string? guardianName = null,
            string? guardianId = null,
            string? guardianProviderName = null,
            string? guardianVersion = null,
            string? targetId = null,
            string? decisionReason = null,
            string? decisionCode = null,
            string? policyId = null,
            string? policyName = null,
            string? policyVersion = null,
            string? contentInputHash = null,
            bool? contentModified = null,
            string? externalEventId = null)
        {
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            DecisionType = decisionType;
            GuardianName = guardianName;
            GuardianId = guardianId;
            GuardianProviderName = guardianProviderName;
            GuardianVersion = guardianVersion;
            TargetId = targetId;
            DecisionReason = decisionReason;
            DecisionCode = decisionCode;
            PolicyId = policyId;
            PolicyName = policyName;
            PolicyVersion = policyVersion;
            ContentInputHash = contentInputHash;
            ContentModified = contentModified;
            ExternalEventId = externalEventId;
        }

        /// <summary>
        /// Gets the type of content or action the guardrail is applied to.
        /// </summary>
        /// <seealso cref="GuardrailTargetType"/>
        public string TargetType { get; }

        /// <summary>
        /// Gets the decision made by the security guardian.
        /// </summary>
        public GuardrailDecisionType DecisionType { get; }

        /// <summary>
        /// Gets the human-readable name of the guardian.
        /// </summary>
        public string? GuardianName { get; }

        /// <summary>
        /// Gets the unique identifier of the guardian.
        /// </summary>
        public string? GuardianId { get; }

        /// <summary>
        /// Gets the provider of the guardian service.
        /// </summary>
        public string? GuardianProviderName { get; }

        /// <summary>
        /// Gets the version of the guardian.
        /// </summary>
        public string? GuardianVersion { get; }

        /// <summary>
        /// Gets the identifier of the target being guarded.
        /// </summary>
        public string? TargetId { get; }

        /// <summary>
        /// Gets the human-readable explanation for the decision.
        /// </summary>
        public string? DecisionReason { get; }

        /// <summary>
        /// Gets the machine-readable decision code.
        /// </summary>
        public string? DecisionCode { get; }

        /// <summary>
        /// Gets the identifier of the policy that triggered the decision.
        /// </summary>
        public string? PolicyId { get; }

        /// <summary>
        /// Gets the human-readable name of the policy.
        /// </summary>
        public string? PolicyName { get; }

        /// <summary>
        /// Gets the version of the policy.
        /// </summary>
        public string? PolicyVersion { get; }

        /// <summary>
        /// Gets the hash of the input content for forensic correlation.
        /// </summary>
        public string? ContentInputHash { get; }

        /// <summary>
        /// Gets whether content was modified by the guardrail.
        /// </summary>
        public bool? ContentModified { get; }

        /// <summary>
        /// Gets the external correlation identifier for SIEM systems.
        /// </summary>
        public string? ExternalEventId { get; }

        /// <inheritdoc/>
        public bool Equals(GuardrailDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(TargetType, other.TargetType, StringComparison.Ordinal) &&
                   DecisionType == other.DecisionType &&
                   string.Equals(GuardianName, other.GuardianName, StringComparison.Ordinal) &&
                   string.Equals(GuardianId, other.GuardianId, StringComparison.Ordinal) &&
                   string.Equals(GuardianProviderName, other.GuardianProviderName, StringComparison.Ordinal) &&
                   string.Equals(GuardianVersion, other.GuardianVersion, StringComparison.Ordinal) &&
                   string.Equals(TargetId, other.TargetId, StringComparison.Ordinal) &&
                   string.Equals(DecisionReason, other.DecisionReason, StringComparison.Ordinal) &&
                   string.Equals(DecisionCode, other.DecisionCode, StringComparison.Ordinal) &&
                   string.Equals(PolicyId, other.PolicyId, StringComparison.Ordinal) &&
                   string.Equals(PolicyName, other.PolicyName, StringComparison.Ordinal) &&
                   string.Equals(PolicyVersion, other.PolicyVersion, StringComparison.Ordinal) &&
                   string.Equals(ContentInputHash, other.ContentInputHash, StringComparison.Ordinal) &&
                   ContentModified == other.ContentModified &&
                   string.Equals(ExternalEventId, other.ExternalEventId, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as GuardrailDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (TargetType != null ? StringComparer.Ordinal.GetHashCode(TargetType) : 0);
                hash = (hash * 31) + DecisionType.GetHashCode();
                hash = (hash * 31) + (GuardianName != null ? StringComparer.Ordinal.GetHashCode(GuardianName) : 0);
                hash = (hash * 31) + (GuardianId != null ? StringComparer.Ordinal.GetHashCode(GuardianId) : 0);
                hash = (hash * 31) + (GuardianProviderName != null ? StringComparer.Ordinal.GetHashCode(GuardianProviderName) : 0);
                hash = (hash * 31) + (GuardianVersion != null ? StringComparer.Ordinal.GetHashCode(GuardianVersion) : 0);
                hash = (hash * 31) + (TargetId != null ? StringComparer.Ordinal.GetHashCode(TargetId) : 0);
                hash = (hash * 31) + (DecisionReason != null ? StringComparer.Ordinal.GetHashCode(DecisionReason) : 0);
                hash = (hash * 31) + (DecisionCode != null ? StringComparer.Ordinal.GetHashCode(DecisionCode) : 0);
                hash = (hash * 31) + (PolicyId != null ? StringComparer.Ordinal.GetHashCode(PolicyId) : 0);
                hash = (hash * 31) + (PolicyName != null ? StringComparer.Ordinal.GetHashCode(PolicyName) : 0);
                hash = (hash * 31) + (PolicyVersion != null ? StringComparer.Ordinal.GetHashCode(PolicyVersion) : 0);
                hash = (hash * 31) + (ContentInputHash != null ? StringComparer.Ordinal.GetHashCode(ContentInputHash) : 0);
                hash = (hash * 31) + (ContentModified?.GetHashCode() ?? 0);
                hash = (hash * 31) + (ExternalEventId != null ? StringComparer.Ordinal.GetHashCode(ExternalEventId) : 0);
                return hash;
            }
        }
    }
}
