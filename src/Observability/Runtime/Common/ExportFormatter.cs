// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Common
{
    /// <summary>
    /// Provides functionality to format Activity spans into OTLP JSON payloads.
    /// </summary>
    public class ExportFormatter
    {
        private const int MaxSpanSizeBytes = 250 * 1024;
        private static readonly string[] LargePayloadAttributeKeys = new[]
        {
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiEventContent,
            OpenTelemetryConstants.GenAiInputMessagesKey,
            OpenTelemetryConstants.GenAiInvocationInputKey,
            OpenTelemetryConstants.GenAiOutputMessagesKey,
            OpenTelemetryConstants.GenAiInvocationOutputKey
        };

        /// <summary>
        /// Formats a collection of Activity spans into an OTLP JSON payload compatible with the Agent 365 Observability ingestion service.
        /// </summary>
        /// <param name="activities">The collection of Activity spans to be formatted into the OTLP payload.</param>
        /// <param name="resource">The OpenTelemetry resource associated with the spans, containing resource attributes.</param>
        /// <param name="logInformation">An optional logger for informational messages.</param>
        /// <returns>A JSON string representing the OTLP payload for the provided activities and resource.</returns>
        public static string FormatMany(IEnumerable<Activity> activities, Resource resource, Action<string>? logInformation = null)
        {
            var resourceAttributes = GetResourceAttributes(resource);
            var serviceName = GetServiceName(resource);
            var serviceVersion = GetServiceVersion(resource);

            var scopeMap = new Dictionary<(string Name, string? Version), List<OtlpSpan>>();

            foreach (var activity in activities)
            {
                var key = (activity.Source.Name, activity.Source.Version);
                if (!scopeMap.TryGetValue(key, out var spans))
                {
                    spans = new List<OtlpSpan>();
                    scopeMap[key] = spans;
                }
                spans.Add(BuildOtlpSpanWithTruncation(activity: activity, logInformation: logInformation));
            }

            var scopeSpans = new List<ScopeSpans>(scopeMap.Count);
            foreach (var kv in scopeMap)
            {
                scopeSpans.Add(new ScopeSpans
                {
                    Scope = new InstrumentationScope
                    {
                        Name = kv.Key.Name,
                        Version = kv.Key.Version
                    },
                    Spans = kv.Value
                });
            }

            var resourceAttrs = MapResourceAttributes(resourceAttributes, serviceName, serviceVersion);

            var payload = new ExportTraceServicePayload
            {
                ResourceSpans = new List<ResourceSpans>
                {
                    new ResourceSpans
                    {
                        Resource = new OtlpResource { Attributes = resourceAttrs },
                        ScopeSpans = scopeSpans
                    }
                }
            };

            return SerializePayload(payload);
        }

        /// <summary>
        /// Formats a single Activity span into an OTLP JSON payload compatible with the Agent 365 Observability ingestion service.
        /// </summary>
        /// <param name="activity">The Activity span to be formatted into the OTLP payload.</param>
        /// <param name="resource">The OpenTelemetry resource associated with the span, containing resource attributes.</param>
        /// <returns>A JSON string representing the OTLP payload for the provided activity and resource.</returns>
        public static string FormatSingle(Activity activity, Resource resource)
        {
            var resourceAttributes = GetResourceAttributes(resource);
            var serviceName = GetServiceName(resource);
            var serviceVersion = GetServiceVersion(resource);

            var resourceAttrs = MapResourceAttributes(resourceAttributes, serviceName, serviceVersion);

            var payload = new ExportTraceEtwPayload
            {
                ResourceSpan = new EtwResourceSpan()
                {
                    Resource = new OtlpResource { Attributes = resourceAttrs },
                    ScopeSpan = new EtwScopeSpan()
                    {
                        Scope = new InstrumentationScope
                        {
                            Name = activity.Source.Name,
                            Version = activity.Source.Version
                        },
                        Span = BuildOtlpSpan(activity)
                    }
                }
            };

            return SerializePayload(payload);
        }

        /// <summary>
        /// Formats the log data for the OTLP payload.
        /// </summary>
        /// <param name="data">The operation data containing the log information.</param>
        /// <returns>A JSON string representing the OTLP payload for the log data.</returns>
        public static string FormatLogData(IDictionary<string, object?> data)
        {
            var payload = new
            {
                Name = data["Name"],
                Attributes = data["Attributes"],
                StartTimeUnixNano = data.TryGetValue("StartTime", out var startTimeObj) && startTimeObj != null ? ToUnixNanos(((DateTimeOffset)startTimeObj).UtcDateTime) : 0,
                EndTimeUnixNano = data.TryGetValue("EndTime", out var endTimeObj) && endTimeObj != null ? ToUnixNanos(((DateTimeOffset)endTimeObj).UtcDateTime) : 0,
                SpanId = data["SpanId"],
                ParentSpanId = data["ParentSpanId"]
            };

            return SerializePayload(payload);
        }

        private static Dictionary<string, object?> GetResourceAttributes(Resource resource)
        {
            var resourceAttributes = new Dictionary<string, object?>();
            foreach (var kvp in resource.Attributes)
            {
                resourceAttributes[kvp.Key] = kvp.Value;
            }
            return resourceAttributes;
        }

        private static string? GetServiceName(Resource resource)
        {
            return resource.Attributes.FirstOrDefault(a => a.Key == "service.name").Value?.ToString();
        }

        private static string? GetServiceVersion(Resource resource)
        {
            return resource.Attributes.FirstOrDefault(a => a.Key == "service.version").Value?.ToString();
        }

        private static OtlpSpan BuildOtlpSpanWithTruncation(Activity activity, Action<string>? logInformation = null)
        {
            var span = BuildOtlpSpan(activity);

            // Check initial size
            if (Encoding.UTF8.GetByteCount(ExportFormatter.SerializePayload(span)) <= ExportFormatter.MaxSpanSizeBytes)
                return span;

            // Gather key sizes
            var keySizes = new List<(string Key, int Size, string? Value)>();
            foreach (var key in ExportFormatter.LargePayloadAttributeKeys)
            {
                if (span.Attributes != null && span.Attributes.TryGetValue(key, out var valueObj))
                {
                    var value = valueObj as string;
                    int size = !string.IsNullOrEmpty(value) ? Encoding.UTF8.GetByteCount(value) : 0;
                    keySizes.Add((key, size, value));
                    logInformation?.Invoke($"Activity '{activity.DisplayName}': Key '{key}' size = {size / 1024} KB.");
                }
            }

            // Sort keys by size descending
            var sorted = keySizes
                .Where(k => !string.IsNullOrEmpty(k.Value) && k.Size > 0)
                .OrderByDescending(k => k.Size)
                .ToList();

            foreach (var (key, size, _) in sorted)
            {
                if (span.Attributes != null)
                {
                    span.Attributes[key] = "TRUNCATED";
                    logInformation?.Invoke($"Truncated '{key}' in activity '{activity.DisplayName}' to reduce size. Previous size: {size / 1024} KB.");
                }

                // Re-check size after each truncation
                var json = SerializePayload(span);
                if (Encoding.UTF8.GetByteCount(json) <= ExportFormatter.MaxSpanSizeBytes)
                {
                    break;
                }
            }

            return span;
        }

        private static OtlpSpan BuildOtlpSpan(Activity activity)
        {
            return new OtlpSpan
            {
                TraceId = ToHex(activity.TraceId),
                SpanId = ToHex(activity.SpanId),
                ParentSpanId = activity.ParentSpanId != default ? ToHex(activity.ParentSpanId) : null,
                Name = activity.DisplayName,
                Kind = activity.Kind,
                StartTimeUnixNano = ToUnixNanos(activity.StartTimeUtc),
                EndTimeUnixNano = ToUnixNanos(activity.StartTimeUtc + activity.Duration),
                Attributes = MapAttributes(activity),
                Events = MapEvents(activity),
                Links = MapLinks(activity),
                Status = new Dictionary<string, object>
                {
                    { "code", activity.Status },
                    { "message", activity.StatusDescription ?? "" }
                }
            };
        }

        private static string SerializePayload<T>(T payload)
        {
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }

        private static string ToHex(ActivityTraceId id)
        {
            return id.ToHexString().ToLowerInvariant();
        }

        private static string ToHex(ActivitySpanId id)
        {
            return id.ToHexString().ToLowerInvariant();
        }

        private static ulong ToUnixNanos(DateTime utc)
        {
            var dt = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ns = (dt - unixEpoch).Ticks * 100;
            return (ulong)ns;
        }

        private static Dictionary<string, object> MapAttributes(Activity activity)
        {
            var dict = new Dictionary<string, object>();

            foreach (var tag in activity.TagObjects)
            {
                dict.Add(tag.Key, tag.Value ?? "");
            }

            return dict;
        }

        private static List<OtlpEvent>? MapEvents(Activity activity)
        {
            if (activity.Events is null) return null;
            var events = new List<OtlpEvent>();
            foreach (var ev in activity.Events)
            {
                var attrs = new Dictionary<string, object>();
                foreach (var tag in ev.Tags)
                {
                    attrs.Add(tag.Key, tag.Value ?? "");
                }

                events.Add(new OtlpEvent
                {
                    TimeUnixNano = ToUnixNanos(ev.Timestamp.UtcDateTime),
                    Name = ev.Name,
                    Attributes = attrs.Count > 0 ? attrs : null
                });
            }
            return events.Count > 0 ? events : null;
        }

        private static List<OtlpLink>? MapLinks(Activity activity)
        {
            if (activity.Links is null) return null;
            var links = new List<OtlpLink>();
            foreach (var link in activity.Links)
            {
                var attrs = new Dictionary<string, object>();
                if (link.Tags != null)
                {
                    foreach (var tag in link.Tags)
                    {
                        attrs.Add(tag.Key, tag.Value ?? "");
                    }
                }

                links.Add(new OtlpLink
                {
                    TraceId = ToHex(link.Context.TraceId),
                    SpanId = ToHex(link.Context.SpanId),
                    Attributes = attrs.Count > 0 ? attrs : null
                });
            }
            return links.Count > 0 ? links : null;
        }

        private static Dictionary<string, object> MapResourceAttributes(
            IReadOnlyDictionary<string, object?> attrs,
            string? serviceName,
            string? serviceVersion)
        {
            var dict = new Dictionary<string, object>();
            foreach (var kvp in attrs)
            {
                dict.Add(kvp.Key, kvp.Value ?? "");
            }

            return dict;
        }
    }

    #region OTLP JSON DTOs (snake_case with JsonPropertyName)

    // Root request
    internal sealed class ExportTraceServicePayload
    {
        [JsonPropertyName("resourceSpans")]
        public List<ResourceSpans> ResourceSpans { get; set; } = new List<ResourceSpans>();
    }

    internal sealed class ExportTraceEtwPayload
    {
        [JsonPropertyName("resourceSpan")]
        public EtwResourceSpan ResourceSpan { get; set; } = new EtwResourceSpan();
    }

    internal sealed class EtwResourceSpan
    {
        [JsonPropertyName("resource")]
        public OtlpResource? Resource { get; set; }
        
        [JsonPropertyName("scopeSpan")]
        public EtwScopeSpan ScopeSpan { get; set; } = new EtwScopeSpan();
    }

    internal sealed class EtwScopeSpan
    {
        [JsonPropertyName("scope")]
        public InstrumentationScope? Scope { get; set; }
     
        [JsonPropertyName("span")]
        public OtlpSpan Span { get; set; } = new OtlpSpan();
    }

    internal sealed class ResourceSpans
    {
        [JsonPropertyName("resource")]
        public OtlpResource? Resource { get; set; }

        [JsonPropertyName("scopeSpans")]
        public List<ScopeSpans> ScopeSpans { get; set; } = new List<ScopeSpans>();
    }

    internal sealed class OtlpResource
    {
        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; }
    }

    internal sealed class ScopeSpans
    {
        [JsonPropertyName("scope")]
        public InstrumentationScope? Scope { get; set; }

        [JsonPropertyName("spans")]
        public List<OtlpSpan> Spans { get; set; } = new List<OtlpSpan>();
    }

    internal sealed class InstrumentationScope
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    internal sealed class OtlpSpan
    {
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = default!; // 32-char hex

        [JsonPropertyName("spanId")]
        public string SpanId { get; set; } = default!; // 16-char hex

        [JsonPropertyName("parentSpanId")]
        public string? ParentSpanId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("kind")]
        public ActivityKind? Kind { get; set; }

        [JsonPropertyName("startTimeUnixNano")]
        public ulong StartTimeUnixNano { get; set; }

        [JsonPropertyName("endTimeUnixNano")]
        public ulong EndTimeUnixNano { get; set; }

        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; }

        [JsonPropertyName("events")]
        public List<OtlpEvent>? Events { get; set; }

        [JsonPropertyName("links")]
        public List<OtlpLink>? Links { get; set; }

        [JsonPropertyName("status")]
        public Dictionary<string, object>? Status { get; set; }
    }

    internal sealed class OtlpEvent
    {
        [JsonPropertyName("timeUnixNano")]
        public ulong TimeUnixNano { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; }
    }

    internal sealed class OtlpLink
    {
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = default!;

        [JsonPropertyName("spanId")]
        public string SpanId { get; set; } = default!;

        [JsonPropertyName("attributes")]
        public Dictionary<string, object>? Attributes { get; set; }
    }

    internal sealed class OtlpStatus
    {
        // STATUS_CODE_UNSET | STATUS_CODE_OK | STATUS_CODE_ERROR
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
    #endregion
}
