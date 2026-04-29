// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Heuristic span size estimation and byte-level chunking for OTLP/HTTP JSON payloads.
    ///
    /// The Agent 365 OTLP traces endpoint enforces a 1 MB request body limit. Per-span
    /// truncation (250 KB cap inside <see cref="Common.ExportFormatter"/>) prevents any
    /// single span from being too large; this class provides the second layer of defense
    /// at the batch level by splitting batches into sub-batches whose cumulative estimated
    /// size stays under <see cref="Agent365ExporterOptions.MaxPayloadBytes"/>.
    /// </summary>
    public static class PayloadChunking
    {
        /// <summary>
        /// Overhead constant for OTLP JSON span fixed fields
        /// (traceId, spanId, parentSpanId, kind, timestamps, status, scope wrapper, etc.).
        /// Intentionally generous to account for serializer variance.
        /// </summary>
        private const int SpanBaseOverhead = 2000;

        /// <summary>
        /// Overhead per attribute in OTLP JSON format. Covers key/value JSON wrapping overhead.
        /// </summary>
        private const int AttrOverhead = 80;

        /// <summary>
        /// Overhead per event in OTLP JSON.
        /// </summary>
        private const int EventOverhead = 200;

        /// <summary>
        /// Estimates the serialized byte size of a single attribute value in OTLP/HTTP JSON.
        /// </summary>
        /// <param name="value">The attribute value to estimate.</param>
        /// <returns>An over-estimate of the serialized size in bytes.</returns>
        public static long EstimateValueBytes(object? value)
        {
            if (value is string s)
            {
                return 40 + Encoding.UTF8.GetByteCount(s);
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                long count = 0;
                bool firstIsString = false;
                long stringSum = 0;
                bool sawAny = false;
                foreach (var item in enumerable)
                {
                    if (!sawAny)
                    {
                        firstIsString = item is string;
                        sawAny = true;
                    }
                    if (firstIsString)
                    {
                        stringSum += 40 + Encoding.UTF8.GetByteCount(item?.ToString() ?? string.Empty);
                    }
                    count++;
                }

                if (count == 0) return 60;
                if (firstIsString) return 60 + stringSum;
                return 60 + 50 * count;
            }

            return 40; // bool/int/float/null/other
        }

        /// <summary>
        /// Heuristic estimator for the serialized size of an OTLP span produced from the
        /// supplied <see cref="Activity"/> when emitted as OTLP/HTTP JSON.
        ///
        /// Uses generous constants tuned to over-estimate by ~25-50%, providing headroom
        /// for JSON serializer variance (whitespace, enum representation, integer-as-string).
        /// </summary>
        /// <param name="activity">The activity to estimate.</param>
        /// <returns>An over-estimate of the serialized OTLP span size in bytes.</returns>
        public static long EstimateActivityBytes(Activity activity)
        {
            if (activity is null) throw new ArgumentNullException(nameof(activity));

            long total = SpanBaseOverhead;

            if (!string.IsNullOrEmpty(activity.DisplayName))
            {
                total += Encoding.UTF8.GetByteCount(activity.DisplayName);
            }

            foreach (var tag in activity.TagObjects)
            {
                total += AttrOverhead;
                total += Encoding.UTF8.GetByteCount(tag.Key ?? string.Empty);
                total += EstimateValueBytes(tag.Value);
            }

            if (activity.Events != null)
            {
                foreach (var ev in activity.Events)
                {
                    total += EventOverhead;
                    total += Encoding.UTF8.GetByteCount(ev.Name ?? string.Empty);
                    if (ev.Tags != null)
                    {
                        foreach (var tag in ev.Tags)
                        {
                            total += AttrOverhead;
                            total += Encoding.UTF8.GetByteCount(tag.Key ?? string.Empty);
                            total += EstimateValueBytes(tag.Value);
                        }
                    }
                }
            }

            return total;
        }

        /// <summary>
        /// Splits items into sub-batches whose cumulative estimated size stays under
        /// <paramref name="maxChunkBytes"/>.
        ///
        /// Invariants:
        /// <list type="bullet">
        ///   <item><description>Input order is preserved across chunks.</description></item>
        ///   <item><description>Empty input produces empty output.</description></item>
        ///   <item><description>A single item whose size exceeds <paramref name="maxChunkBytes"/>
        ///   forms its own single-item chunk (never silently dropped).</description></item>
        ///   <item><description>No chunk is ever empty.</description></item>
        /// </list>
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">The items to chunk.</param>
        /// <param name="getSize">Estimator function returning approximate serialized byte size of one item.</param>
        /// <param name="maxChunkBytes">Upper bound on cumulative estimated size per chunk.</param>
        /// <returns>An ordered list of non-empty chunks.</returns>
        public static List<List<T>> ChunkBySize<T>(IEnumerable<T> items, Func<T, long> getSize, long maxChunkBytes)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (getSize is null) throw new ArgumentNullException(nameof(getSize));

            var chunks = new List<List<T>>();
            var current = new List<T>();
            long currentBytes = 0;

            foreach (var item in items)
            {
                long itemBytes = getSize(item);
                if (current.Count > 0 && currentBytes + itemBytes > maxChunkBytes)
                {
                    chunks.Add(current);
                    current = new List<T>();
                    currentBytes = 0;
                }
                current.Add(item);
                currentBytes += itemBytes;
            }

            if (current.Count > 0)
            {
                chunks.Add(current);
            }

            return chunks;
        }
    }
}
