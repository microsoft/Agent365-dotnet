// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Groups span configuration for scope construction.
    /// </summary>
    public sealed class SpanDetails
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanDetails"/> class.
        /// </summary>
        /// <param name="spanKind">Optional span kind override.</param>
        /// <param name="parentContext">Optional parent <see cref="ActivityContext"/> used to link this span to an upstream operation.
        /// Use <see cref="Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.TraceContextHelper.ExtractContextFromHeaders"/>
        /// to obtain an <see cref="ActivityContext"/> from HTTP headers containing a W3C traceparent.</param>
        /// <param name="startTime">Optional explicit start time as a <see cref="DateTimeOffset"/>.</param>
        /// <param name="endTime">Optional explicit end time as a <see cref="DateTimeOffset"/>.</param>
        /// <param name="spanLinks">Optional span links to associate with this span, establishing causal
        /// relationships to other spans (e.g. linking a batch operation to individual trigger spans).</param>
        public SpanDetails(
            ActivityKind? spanKind = null,
            ActivityContext? parentContext = null,
            DateTimeOffset? startTime = null,
            DateTimeOffset? endTime = null,
            ActivityLink[]? spanLinks = null)
        {
            SpanKind = spanKind;
            ParentContext = parentContext;
            StartTime = startTime;
            EndTime = endTime;
            SpanLinks = spanLinks;
        }

        /// <summary>
        /// Gets the optional span kind override.
        /// </summary>
        public ActivityKind? SpanKind { get; }

        /// <summary>
        /// Gets the optional OpenTelemetry parent context used to link this span to an upstream operation.
        /// </summary>
        public ActivityContext? ParentContext { get; }

        /// <summary>
        /// Gets the optional explicit start time.
        /// </summary>
        public DateTimeOffset? StartTime { get; }

        /// <summary>
        /// Gets the optional explicit end time.
        /// </summary>
        public DateTimeOffset? EndTime { get; }

        /// <summary>
        /// Gets the optional span links to associate with this span.
        /// </summary>
        public ActivityLink[]? SpanLinks { get; }
    }
}
