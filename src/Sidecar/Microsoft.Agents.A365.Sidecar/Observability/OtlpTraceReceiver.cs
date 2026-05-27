// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Observability;

/// <summary>
/// Receives OTLP/HTTP trace data from customer applications, enriches spans with
/// Agent365 identity tags, and forwards them through the A365 exporter pipeline.
/// </summary>
public sealed class OtlpTraceReceiver
{
    private readonly Agent365ExporterCore _exporterCore;
    private readonly ExportFormatter _formatter;
    private readonly SidecarOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OtlpTraceReceiver> _logger;
    private readonly Agent365ExporterOptions _exporterOptions;

    /// <summary>
    /// Initializes a new instance of <see cref="OtlpTraceReceiver"/>.
    /// </summary>
    public OtlpTraceReceiver(
        Agent365ExporterCore exporterCore,
        ExportFormatter formatter,
        IOptions<SidecarOptions> options,
        IOptions<Agent365ExporterOptions> exporterOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<OtlpTraceReceiver> logger)
    {
        _exporterCore = exporterCore;
        _formatter = formatter;
        _options = options.Value;
        _exporterOptions = exporterOptions.Value;
        _httpClient = httpClientFactory.CreateClient("ObservabilityExporter");
        _logger = logger;
    }

    /// <summary>
    /// Processes incoming OTLP/HTTP trace data.
    /// </summary>
    /// <param name="body">The raw request body bytes (JSON or protobuf).</param>
    /// <param name="contentType">The Content-Type header value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and span count.</returns>
    public async Task<TraceReceiveResult> ReceiveTracesAsync(byte[] body, string contentType, CancellationToken cancellationToken)
    {
        if (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Unsupported content type: {ContentType}. Only application/json is supported.", contentType);
            return TraceReceiveResult.Failure(415, "Only application/json content type is supported");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<JsonElement>(body);
            var spanCount = CountSpans(payload);

            if (spanCount == 0)
            {
                _logger.LogDebug("Received trace payload with no spans");
                return TraceReceiveResult.Success(0);
            }

            // Enrich and forward the trace payload
            var enrichedPayload = EnrichPayload(payload);
            await ForwardToAgent365Async(enrichedPayload, cancellationToken);

            _logger.LogInformation("Forwarded {SpanCount} spans to Agent365 observability", spanCount);
            return TraceReceiveResult.Success(spanCount);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OTLP trace payload");
            return TraceReceiveResult.Failure(400, "Invalid JSON payload");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to forward traces to Agent365 endpoint");
            return TraceReceiveResult.Failure(502, "Failed to forward to Agent365");
        }
    }

    private static int CountSpans(JsonElement payload)
    {
        int count = 0;
        if (payload.TryGetProperty("resourceSpans", out var resourceSpans))
        {
            foreach (var rs in resourceSpans.EnumerateArray())
            {
                if (rs.TryGetProperty("scopeSpans", out var scopeSpans))
                {
                    foreach (var ss in scopeSpans.EnumerateArray())
                    {
                        if (ss.TryGetProperty("spans", out var spans))
                        {
                            count += spans.GetArrayLength();
                        }
                    }
                }
            }
        }

        return count;
    }

    private JsonElement EnrichPayload(JsonElement payload)
    {
        // Clone and inject agent/tenant identity attributes into resource attributes
        using var doc = JsonDocument.Parse(payload.GetRawText());
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();

        if (payload.TryGetProperty("resourceSpans", out var resourceSpans))
        {
            writer.WritePropertyName("resourceSpans");
            writer.WriteStartArray();

            foreach (var rs in resourceSpans.EnumerateArray())
            {
                writer.WriteStartObject();

                // Enrich resource with A365 identity attributes
                writer.WritePropertyName("resource");
                WriteEnrichedResource(writer, rs);

                // Copy scopeSpans as-is
                if (rs.TryGetProperty("scopeSpans", out var scopeSpans))
                {
                    writer.WritePropertyName("scopeSpans");
                    scopeSpans.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();

        return JsonDocument.Parse(ms.ToArray()).RootElement.Clone();
    }

    private void WriteEnrichedResource(Utf8JsonWriter writer, JsonElement resourceSpan)
    {
        writer.WriteStartObject();

        JsonElement existingAttrs = default;
        bool hasExistingAttrs = false;

        if (resourceSpan.TryGetProperty("resource", out var resource))
        {
            // Copy existing resource properties except attributes
            foreach (var prop in resource.EnumerateObject())
            {
                if (prop.Name == "attributes")
                {
                    existingAttrs = prop.Value;
                    hasExistingAttrs = true;
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
        }

        // Write enriched attributes
        writer.WritePropertyName("attributes");
        writer.WriteStartArray();

        // Copy existing attributes
        if (hasExistingAttrs)
        {
            foreach (var attr in existingAttrs.EnumerateArray())
            {
                attr.WriteTo(writer);
            }
        }

        // Inject A365 identity attributes
        WriteStringAttribute(writer, OpenTelemetryConstants.TenantIdKey, _options.Auth.TenantId);
        WriteStringAttribute(writer, OpenTelemetryConstants.GenAiAgentIdKey, _options.Agent.Id);

        if (!string.IsNullOrEmpty(_options.Auth.ClientId))
        {
            WriteStringAttribute(writer, OpenTelemetryConstants.AgentBlueprintIdKey, _options.Auth.ClientId);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStringAttribute(Utf8JsonWriter writer, string key, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteStartObject("value");
        writer.WriteString("stringValue", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private async Task ForwardToAgent365Async(JsonElement enrichedPayload, CancellationToken cancellationToken)
    {
        var tenantId = _options.Auth.TenantId;
        var agentId = _options.Agent.Id;

        var endpointPath = _exporterCore.BuildEndpointPath(tenantId, agentId, _exporterOptions.UseS2SEndpoint);
        var domain = _exporterOptions.DomainResolver(tenantId);
        var requestUri = _exporterCore.BuildRequestUri(domain, endpointPath);

        var payloadJson = enrichedPayload.GetRawText();
        using var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

        // Resolve auth token
        if (_exporterOptions.TokenResolver != null)
        {
            var token = await _exporterOptions.TokenResolver(agentId, tenantId);
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Result of processing incoming trace data.
/// </summary>
public sealed class TraceReceiveResult
{
    /// <summary>
    /// Whether the trace data was processed successfully.
    /// </summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Number of spans processed.
    /// </summary>
    public int SpanCount { get; private init; }

    /// <summary>
    /// HTTP status code for failure responses.
    /// </summary>
    public int? StatusCode { get; private init; }

    /// <summary>
    /// Error message for failure responses.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TraceReceiveResult Success(int spanCount) => new()
    {
        IsSuccess = true,
        SpanCount = spanCount,
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static TraceReceiveResult Failure(int statusCode, string? message = null) => new()
    {
        IsSuccess = false,
        StatusCode = statusCode,
        ErrorMessage = message,
    };
}
