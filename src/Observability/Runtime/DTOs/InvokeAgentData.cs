using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Encapsulates all telemetry data for an invoke_agent operation, including attributes, timing, and span information.
    /// </summary>
    public struct InvokeAgentData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeAgentData"/> struct.
        /// </summary>
        /// <param name="attributes">The telemetry attributes (tags).</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation. If not provided one will be created.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        public InvokeAgentData(
            IDictionary<string, object?>? attributes = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null)
        {
            Attributes = attributes ?? new Dictionary<string, object?>();
            StartTime = startTime;
            EndTime = endTime;
            SpanId = spanId ?? Guid.NewGuid().ToString();
            ParentSpanId = parentSpanId;
        }

        /// <summary>
        /// Gets the name of the operation.
        /// </summary>
        public readonly string Name => "InvokeAgent";

        /// <summary>
        /// Gets the telemetry attributes (tags) for the invoke_agent operation.
        /// </summary>
        public IDictionary<string, object?> Attributes { get; }

        /// <summary>
        /// Gets the custom start time for the operation, if provided.
        /// </summary>
        public DateTimeOffset? StartTime { get; set; }

        /// <summary>
        /// Gets the custom end time for the operation, if provided.
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }

        /// <summary>
        /// Gets the span ID for the operation, if provided.
        /// </summary>
        public string SpanId { get; }

        /// <summary>
        /// Gets the parent span ID for distributed tracing, if provided.
        /// </summary>
        public string? ParentSpanId { get; }

        /// <summary>
        /// Gets the duration of the operation if both start and end times are provided.
        /// </summary>
        public TimeSpan Duration => StartTime.HasValue && EndTime.HasValue
            ? EndTime.Value - StartTime.Value 
            : TimeSpan.Zero;
    }
}
