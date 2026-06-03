// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Well-known severity levels for security risks detected by guardrails.
    /// </summary>
    public static class GuardrailRiskSeverity
    {
        /// <summary>
        /// No risk detected.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Low severity risk.
        /// </summary>
        public const string Low = "low";

        /// <summary>
        /// Medium severity risk.
        /// </summary>
        public const string Medium = "medium";

        /// <summary>
        /// High severity risk.
        /// </summary>
        public const string High = "high";

        /// <summary>
        /// Critical severity risk requiring immediate action.
        /// </summary>
        public const string Critical = "critical";
    }
}
