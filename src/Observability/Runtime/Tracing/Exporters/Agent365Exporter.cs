using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Minimal OTLP/HTTP JSON exporter for traces.
    /// Sends POST {Endpoint}/v1/traces with application/json.
    /// </summary>
    public sealed class Agent365Exporter : BaseExporter<Activity>
    {
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IReadOnlyDictionary<string, object?> _resourceAttributes;
    private readonly string? _serviceName;
    private readonly string? _serviceVersion;
    private readonly ILogger<Agent365Exporter> _logger;
    private readonly Agent365ExporterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Agent365Exporter"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The exporter configuration options.</param>
    /// <param name="resource">Optional OpenTelemetry resource information.</param>
    public Agent365Exporter(ILogger<Agent365Exporter> logger, Agent365ExporterOptions options, Resource? resource = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (_options.TokenResolver is null)
            throw new ArgumentNullException(nameof(options.TokenResolver), "Agent365ExporterOptions.TokenResolver must be provided.");
        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = null, // We use explicit JsonPropertyName with OTLP snake_case.
            WriteIndented = false
        };

        var res = resource ?? ResourceBuilder.CreateEmpty().Build();
        var resDict = new Dictionary<string, object?>();
        foreach (var kvp in res.Attributes)
        {
            resDict[kvp.Key] = kvp.Value;
        }
        _resourceAttributes = resDict;

        // Common service.* attributes (if present) are surfaced explicitly in many backends
        _serviceName = res.Attributes.FirstOrDefault(a => a.Key == "service.name").Value?.ToString();
        _serviceVersion = res.Attributes.FirstOrDefault(a => a.Key == "service.version").Value?.ToString();
    }

    /// <summary>
    /// Exports a batch of OpenTelemetry activities to the Kairo observability platform.
    /// </summary>
    /// <param name="batch">The batch of activities to export.</param>
    /// <returns>The export result indicating success or failure.</returns>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        var anyFailure = false;

        try
        {
            // Partition by (tenantId, agentId)
            var groups = PartitionByIdentity(batch);
            if (groups.Count == 0)
            {
                _logger.LogDebug("Agent365Exporter: No spans with tenant/agent identity found; nothing exported.");
                return ExportResult.Success;
            }

            foreach (var g in groups)
            {
                var (tenantId, agentId, activities) = g;

                // Build payload for just this identity
                var payload = BuildExportRequest(activities);
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Endpoint/token per identity
                var ppapiDiscovery = new PowerPlatformApiDiscovery(_options.ClusterCategory);
                var ppapiEndpoint = ppapiDiscovery.GetTenantIslandClusterEndpoint(tenantId);
                
                // Choose endpoint path based on UseS2SEndpoint setting
                var endpointPath = _options.UseS2SEndpoint
                    ? $"/maven/agent365/service/agents/{agentId}/traces"
                    : $"/maven/agent365/agents/{agentId}/traces";
                
                var requestUri = $"https://{ppapiEndpoint}{endpointPath}?api-version=1";

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = content
                };

                string? token = null;
                try
                {
                    token = _options.TokenResolver!(agentId, tenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Agent365Exporter: TokenResolver threw for agent {Agent} tenant {Tenant}.", agentId, tenantId);
                }

                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage? resp = null;
                try
                {
                    resp = _httpClient.SendAsync(request).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        anyFailure = true;
                        _logger.LogWarning("Agent365Exporter: HTTP {Status} exporting spans for agent {Agent} tenant {Tenant}.",
                            (int)resp.StatusCode, agentId, tenantId);
                    }
                }
                catch (Exception ex)
                {
                    anyFailure = true;
                    _logger.LogError(ex, "Agent365Exporter: Exception exporting spans for agent {Agent} tenant {Tenant}.", agentId, tenantId);
                }
                finally
                {
                    resp?.Dispose();
                }
            }
        }
        catch (Exception exOuter)
        {
            _logger.LogError(exOuter, "Agent365Exporter: Unhandled export exception.");
            return ExportResult.Failure;
        }

        return anyFailure ? ExportResult.Failure : ExportResult.Success;
    }

    // Extract (tenant, agent) per activity. Prefer tags; fallback to per-activity baggage.
    private List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(in Batch<Activity> batch)
    {
        var map = new Dictionary<(string tenant, string agent), List<Activity>>();

        foreach (var activity in batch)
        {
            if (activity is null) continue;

            var tenant = activity.GetAttributeOrBaggage(OpenTelemetryConstants.TenantIdKey);
            var agent = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentIdKey);

            if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(agent))
                continue; // skip spans without identity (could log once with a counter)

            // At this point, tenant and agent are guaranteed to be non-null and non-empty
            var key = (tenant!, agent!);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<Activity>();
                map[key] = list;
            }
            list.Add(activity);
        }

        return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
    }

    private ExportTraceServicePayload BuildExportRequest(IEnumerable<Activity> activities)
    {
        var scopeMap = new Dictionary<(string Name, string? Version), List<OtlpSpan>>();

        foreach (var activity in activities)
        {
            var key = (activity.Source.Name, activity.Source.Version);
            if (!scopeMap.TryGetValue(key, out var spans))
            {
                spans = new List<OtlpSpan>();
                scopeMap[key] = spans;
            }

            var span = new OtlpSpan
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

            spans.Add(span);
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

        var resourceAttrs = MapResourceAttributes(_resourceAttributes, _serviceName, _serviceVersion);

        return new ExportTraceServicePayload
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
