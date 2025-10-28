// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Internal data structure for holding span properties independently of System.Diagnostics.Activity.
    /// This allows the SDK to track telemetry data even when Activity is not available or enabled.
    /// </summary>
    public sealed class SpanData
    {
        private readonly Dictionary<string, object?> tags = new Dictionary<string, object?>();
        private readonly Dictionary<string, string> baggage = new Dictionary<string, string>();

        /// <summary>
        /// Gets the kind of span (Client, Server, Internal, etc.).
        /// </summary>
        public ActivityKind Kind { get; }

        /// <summary>
        /// Gets the operation name.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Gets the activity name.
        /// </summary>
        public string ActivityName { get; }

        /// <summary>
        /// Gets the trace ID that identifies the entire trace this span belongs to.
        /// </summary>
        public string? TraceId { get; private set; }

        /// <summary>
        /// Gets the unique identifier for this span.
        /// </summary>
        public string? SpanId { get; private set; }

        /// <summary>
        /// Gets the span ID of the parent span, if any.
        /// </summary>
        public string? ParentSpanId { get; private set; }

        /// <summary>
        /// Gets the status code of the span.
        /// </summary>
        public ActivityStatusCode? StatusCode { get; private set; }

        /// <summary>
        /// Gets the status description.
        /// </summary>
        public string? StatusDescription { get; private set; }

        /// <summary>
        /// Initializes a new instance of the SpanData class.
        /// </summary>
        public SpanData(ActivityKind kind, string operationName, string activityName)
        {
            Kind = kind;
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            ActivityName = activityName ?? throw new ArgumentNullException(nameof(activityName));
        }

        /// <summary>
        /// Sets a tag/attribute on the span.
        /// </summary>
        public void SetTag(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            tags[key] = value;
        }

        /// <summary>
        /// Gets a tag value.
        /// </summary>
        public object? GetTag(string key)
        {
            return tags.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Gets all tags.
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetTags()
        {
            return tags;
        }

        /// <summary>
        /// Adds a baggage item.
        /// </summary>
        public void AddBaggage(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
            {
                return;
            }

            baggage[key] = value;
        }

        /// <summary>
        /// Gets a baggage value.
        /// </summary>
        public string? GetBaggage(string key)
        {
            return baggage.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Gets all baggage items.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetBaggage()
        {
            return baggage;
        }

        /// <summary>
        /// Sets the status of the span.
        /// </summary>
        public void SetStatus(ActivityStatusCode statusCode, string? description = null)
        {
            StatusCode = statusCode;
            StatusDescription = description;
        }

        /// <summary>
        /// Sets the trace and span identifiers for this span.
        /// This establishes the parent-child relationship in distributed tracing.
        /// </summary>
        /// <param name="traceId">The trace ID (identifies the entire trace).</param>
        /// <param name="spanId">The span ID (unique identifier for this span).</param>
        /// <param name="parentSpanId">The parent span ID (optional, identifies the parent span).</param>
        public void SetSpanIdentifiers(string? traceId, string? spanId, string? parentSpanId)
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
        }

        /// <summary>
        /// Sets a tag on the activity only if it does not already exist.
        /// </summary>
        public void CoalesceTag(string key, params string?[] values)
        {
            var tagValue = GetTag(key);
            if (tagValue == null)
            {
                foreach (var value in values)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        SetTag(key, value);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the value of a tag (takes precedence) or baggage item from the activity.
        /// </summary>
        public string? GetAttributeOrBaggage(string key)
        {
            var tagValue = GetTag(key);
            if (tagValue != null)
            {
                return tagValue.ToString();
            }

            var baggageValue = GetBaggage(key);
            return string.IsNullOrEmpty(baggageValue) ? null : baggageValue;
        }
    }
}
