// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Runtime.DTOs
{
    /// <summary>
    /// Encapsulates all telemetry data for an operation, including attributes, timing, and span information.
    /// </summary>
    public abstract class BaseData
    {
        /// <summary>
        /// Initializes a new instance
        /// </summary>
        /// <param name="attributes">The telemetry attributes (tags).</param>
        /// <param name="startTime">Optional custom start time for the operation.</param>
        /// <param name="endTime">Optional custom end time for the operation.</param>
        /// <param name="spanId">Optional span ID for the operation. If not provided one will be created.</param>
        /// <param name="parentSpanId">Optional parent span ID for distributed tracing.</param>
        /// <param name="spanKind">Optional span kind for the operation (e.g., Client, Server, Internal).</param>
        public BaseData(
            IDictionary<string, object?>? attributes = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            string? spanId = null,
            string? parentSpanId = null,
            ActivityKind? spanKind = null)
        {
            Attributes = attributes ?? new Dictionary<string, object?>();
            StartTime = startTime;
            EndTime = endTime;
            // Generate a random span ID if not provided. Use ActivitySpanId for consistency with tracing.
            SpanId = spanId ?? ActivitySpanId.CreateRandom().ToString();
            ParentSpanId = parentSpanId;
            SpanKind = spanKind;
        }

        /// <summary>
        /// Gets the name of the operation.
        /// </summary>
        public abstract string Name { get; }

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
        /// Gets the span kind for the operation, if provided.
        /// </summary>
        public ActivityKind? SpanKind { get; }

        /// <summary>
        /// Gets the duration of the operation if both start and end times are provided.
        /// </summary>
        public TimeSpan Duration => StartTime.HasValue && EndTime.HasValue
            ? EndTime.Value - StartTime.Value
            : TimeSpan.Zero;

        /// <summary>
        /// Converts the telemetry data to a dictionary representation.
        /// </summary>
        public Dictionary<string, object?> ToDictionary()
        {
            var dict = new Dictionary<string, object?>
            {
                { "Name", Name },
                { "Attributes", Attributes },
                { "StartTime", StartTime },
                { "EndTime", EndTime },
                { "SpanId", SpanId },
                { "ParentSpanId", ParentSpanId },
                { "SpanKind", SpanKind },
                { "Duration", Duration }
            };

            return dict;
        }
    }
}
