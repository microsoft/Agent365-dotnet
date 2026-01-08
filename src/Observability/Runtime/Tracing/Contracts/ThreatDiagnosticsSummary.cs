// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a threat diagnostics summary containing security-related information
    /// about blocked actions and their reasons.
    /// </summary>
    public sealed class ThreatDiagnosticsSummary
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreatDiagnosticsSummary"/> class.
        /// </summary>
        /// <param name="blockAction">Indicates whether the action was blocked.</param>
        /// <param name="reasonCode">The numeric reason code for the action.</param>
        /// <param name="reason">A human-readable description of the reason.</param>
        /// <param name="diagnostics">Optional additional diagnostic information.</param>
        public ThreatDiagnosticsSummary(bool blockAction, int reasonCode, string reason, string? diagnostics = null)
        {
            BlockAction = blockAction;
            ReasonCode = reasonCode;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Gets a value indicating whether the action was blocked.
        /// </summary>
        [JsonPropertyName("blockAction")]
        public bool BlockAction { get; }

        /// <summary>
        /// Gets the numeric reason code for the action.
        /// </summary>
        [JsonPropertyName("reasonCode")]
        public int ReasonCode { get; }

        /// <summary>
        /// Gets the human-readable description of the reason.
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; }

        /// <summary>
        /// Gets additional diagnostic information.
        /// </summary>
        [JsonPropertyName("diagnostics")]
        public string? Diagnostics { get; }

        /// <summary>
        /// Serializes this instance to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of this instance.</returns>
        internal string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }
    }
}
