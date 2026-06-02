// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Encapsulates all telemetry data for an apply_guardrail operation.
    /// </summary>
    public class ApplyGuardrailData : BaseData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyGuardrailData"/> class.
        /// </summary>
        /// <param name="parentSpanId">The parent span ID for distributed tracing.</param>
        /// <param name="attributes">The telemetry attributes (tags).</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation. If not provided one will be created.</param>
        /// <param name="spanKind">Optional span kind override. Defaults to <c>null</c> (unset). Use <see cref="SpanKindConstants.Internal"/> as appropriate.</param>
        /// <param name="traceId">Optional trace ID for distributed tracing.</param>
        public ApplyGuardrailData(
            string parentSpanId,
            IDictionary<string, object?>? attributes = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? spanKind = null,
            string? traceId = null)
            : base(attributes, startTime, endTime, spanId, parentSpanId, spanKind, traceId)
        { }

        /// <summary>
        /// Gets the name of the operation.
        /// </summary>
        public override string Name => OpenTelemetryConstants.OperationNames.ApplyGuardrail.ToString();
    }
}
