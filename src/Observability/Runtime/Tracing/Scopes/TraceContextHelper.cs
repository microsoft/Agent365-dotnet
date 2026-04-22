// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes
{
    /// <summary>
    /// Utility methods for W3C trace context propagation via HTTP headers.
    /// </summary>
    public static class TraceContextHelper
    {
        /// <summary>
        /// Extracts an <see cref="ActivityContext"/> from W3C trace HTTP headers.
        /// </summary>
        /// <remarks>
        /// Parses the <c>traceparent</c> (and optionally <c>tracestate</c>) headers
        /// and returns an <see cref="ActivityContext"/> that can be passed as
        /// <c>parentContext</c> to any scope's <c>Start()</c> method.
        /// </remarks>
        /// <param name="headers">Dictionary of HTTP headers containing trace context.
        /// Expected keys include <c>traceparent</c> and optionally <c>tracestate</c>.</param>
        /// <returns>
        /// An <see cref="ActivityContext"/> containing the extracted trace information.
        /// Returns <c>default</c> if no valid trace context is found.
        /// </returns>
        public static ActivityContext ExtractContextFromHeaders(IDictionary<string, string> headers)
        {
            if (headers != null
                && headers.TryGetValue("traceparent", out var traceparent)
                && !string.IsNullOrEmpty(traceparent))
            {
                headers.TryGetValue("tracestate", out var tracestate);
                return ActivityContext.Parse(traceparent, tracestate);
            }

            return default;
        }

        /// <summary>
        /// Returns the W3C <c>traceparent</c> value from a headers dictionary.
        /// </summary>
        /// <param name="headers">Dictionary of HTTP headers, typically obtained from
        /// <see cref="OpenTelemetryScope.InjectTraceContext"/>.</param>
        /// <returns>
        /// The traceparent string (e.g. <c>"00-{trace_id}-{span_id}-{flags}"</c>),
        /// or <c>null</c> if the key is not present.
        /// </returns>
        public static string? GetTraceparent(IDictionary<string, string> headers)
        {
            if (headers != null && headers.TryGetValue("traceparent", out var traceparent))
            {
                return traceparent;
            }

            return null;
        }
    }
}
