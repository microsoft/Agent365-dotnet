// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Represents the OpenTelemetry span status code as defined by the OTLP specification.
    /// The numeric values map directly onto the OTLP <c>Status.StatusCode</c> enum and are
    /// emitted as-is on the ETW telemetry payload.
    /// </summary>
    public enum SpanStatusCode
    {
        /// <summary>
        /// The default status; the span has not been explicitly marked successful or failed.
        /// Maps to OTLP <c>STATUS_CODE_UNSET</c>.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// The operation completed successfully (explicitly set by the application).
        /// Maps to OTLP <c>STATUS_CODE_OK</c>.
        /// </summary>
        Ok = 1,

        /// <summary>
        /// The operation failed. Maps to OTLP <c>STATUS_CODE_ERROR</c>.
        /// </summary>
        Error = 2,
    }
}
