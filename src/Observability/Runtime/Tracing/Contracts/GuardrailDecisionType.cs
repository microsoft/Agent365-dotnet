// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// The decision made by a security guardian during guardrail evaluation.
    /// </summary>
    [DataContract]
    public enum GuardrailDecisionType
    {
        /// <summary>
        /// Content or action is allowed to proceed.
        /// </summary>
        [EnumMember(Value = "allow")]
        Allow,

        /// <summary>
        /// Content or action is logged for review but allowed to proceed.
        /// </summary>
        [EnumMember(Value = "audit")]
        Audit,

        /// <summary>
        /// Content or action is denied/blocked.
        /// </summary>
        [EnumMember(Value = "deny")]
        Deny,

        /// <summary>
        /// Content was modified (e.g., redacted, sanitized, rewritten).
        /// </summary>
        [EnumMember(Value = "modify")]
        Modify,

        /// <summary>
        /// Content or action triggered a warning but is allowed to proceed.
        /// </summary>
        [EnumMember(Value = "warn")]
        Warn
    }
}
