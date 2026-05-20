// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a single security finding detected during guardian evaluation.
    /// Multiple findings may be emitted for a single guardrail span.
    /// </summary>
    public sealed class GuardrailFinding : IEquatable<GuardrailFinding>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GuardrailFinding"/> class.
        /// </summary>
        /// <param name="riskCategory">The category of security risk detected (required).</param>
        /// <param name="riskSeverity">The severity level of the detected risk (required).</param>
        /// <param name="policyDecisionType">The decision type for this specific policy finding.</param>
        /// <param name="policyId">Identifier of the policy that triggered the finding.</param>
        /// <param name="policyName">Human-readable name of the triggered policy.</param>
        /// <param name="policyVersion">Version of the policy.</param>
        /// <param name="riskScore">Numeric risk/confidence score (0.0 to 1.0).</param>
        /// <param name="riskMetadata">Non-content metadata about the detected risk (MUST NOT contain PII).</param>
        public GuardrailFinding(
            string riskCategory,
            string riskSeverity,
            string? policyDecisionType = null,
            string? policyId = null,
            string? policyName = null,
            string? policyVersion = null,
            double? riskScore = null,
            string[]? riskMetadata = null)
        {
            RiskCategory = riskCategory ?? throw new ArgumentNullException(nameof(riskCategory));
            RiskSeverity = riskSeverity ?? throw new ArgumentNullException(nameof(riskSeverity));
            PolicyDecisionType = policyDecisionType;
            PolicyId = policyId;
            PolicyName = policyName;
            PolicyVersion = policyVersion;
            RiskScore = riskScore;
            RiskMetadata = riskMetadata;
        }

        /// <summary>
        /// Gets the category of security risk detected.
        /// </summary>
        /// <remarks>
        /// Free-form field aligned with OWASP LLM Top 10 2025.
        /// Common values: prompt_injection, sensitive_info_disclosure, jailbreak, toxicity, pii.
        /// </remarks>
        public string RiskCategory { get; }

        /// <summary>
        /// Gets the severity level of the detected risk.
        /// </summary>
        /// <seealso cref="GuardrailRiskSeverity"/>
        public string RiskSeverity { get; }

        /// <summary>
        /// Gets the decision type for this specific policy finding.
        /// </summary>
        public string? PolicyDecisionType { get; }

        /// <summary>
        /// Gets the identifier of the policy that triggered the finding.
        /// </summary>
        public string? PolicyId { get; }

        /// <summary>
        /// Gets the human-readable name of the triggered policy.
        /// </summary>
        public string? PolicyName { get; }

        /// <summary>
        /// Gets the version of the policy.
        /// </summary>
        public string? PolicyVersion { get; }

        /// <summary>
        /// Gets the numeric risk/confidence score (0.0 to 1.0).
        /// </summary>
        public double? RiskScore { get; }

        /// <summary>
        /// Gets non-content metadata about the detected risk.
        /// </summary>
        /// <remarks>
        /// This MUST NOT contain sensitive user content, PII, or other high-risk data.
        /// Example values: "field:bcc", "pattern:ssn", "count:3", "position:input[0].content".
        /// </remarks>
        public string[]? RiskMetadata { get; }

        /// <inheritdoc/>
        public bool Equals(GuardrailFinding? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(RiskCategory, other.RiskCategory, StringComparison.Ordinal) &&
                   string.Equals(RiskSeverity, other.RiskSeverity, StringComparison.Ordinal) &&
                   string.Equals(PolicyDecisionType, other.PolicyDecisionType, StringComparison.Ordinal) &&
                   string.Equals(PolicyId, other.PolicyId, StringComparison.Ordinal) &&
                   string.Equals(PolicyName, other.PolicyName, StringComparison.Ordinal) &&
                   string.Equals(PolicyVersion, other.PolicyVersion, StringComparison.Ordinal) &&
                   RiskScore == other.RiskScore;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as GuardrailFinding);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (RiskCategory != null ? StringComparer.Ordinal.GetHashCode(RiskCategory) : 0);
                hash = (hash * 31) + (RiskSeverity != null ? StringComparer.Ordinal.GetHashCode(RiskSeverity) : 0);
                hash = (hash * 31) + (PolicyDecisionType != null ? StringComparer.Ordinal.GetHashCode(PolicyDecisionType) : 0);
                hash = (hash * 31) + (PolicyId != null ? StringComparer.Ordinal.GetHashCode(PolicyId) : 0);
                hash = (hash * 31) + (PolicyName != null ? StringComparer.Ordinal.GetHashCode(PolicyName) : 0);
                hash = (hash * 31) + (PolicyVersion != null ? StringComparer.Ordinal.GetHashCode(PolicyVersion) : 0);
                hash = (hash * 31) + (RiskScore?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
