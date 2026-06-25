// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Represents an OpenTelemetry span status (<c>code</c> and optional <c>message</c>),
    /// following the OTel specification where <see cref="SpanStatusCode.Unset"/> = 0,
    /// <see cref="SpanStatusCode.Ok"/> = 1, and <see cref="SpanStatusCode.Error"/> = 2.
    /// </summary>
    public readonly struct SpanStatus
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanStatus"/> struct.
        /// </summary>
        /// <param name="code">The status code. Defaults to <see cref="SpanStatusCode.Unset"/>.</param>
        /// <param name="message">Optional status message. Per the OTel spec this is only meaningful for an error status.</param>
        public SpanStatus(SpanStatusCode code = SpanStatusCode.Unset, string? message = null)
        {
            Code = code;
            Message = message;
        }

        /// <summary>
        /// Gets the status code (<see cref="SpanStatusCode.Unset"/>, <see cref="SpanStatusCode.Ok"/>, or <see cref="SpanStatusCode.Error"/>).
        /// </summary>
        public SpanStatusCode Code { get; }

        /// <summary>
        /// Gets the optional status message. Only populated for an error status.
        /// </summary>
        public string? Message { get; }
    }
}
