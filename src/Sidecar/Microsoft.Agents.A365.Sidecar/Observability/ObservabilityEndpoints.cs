// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Compression;
using Microsoft.Agents.A365.Sidecar.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Agents.A365.Sidecar.Observability;

/// <summary>
/// Extension methods for mapping Observability API endpoints.
/// The sidecar accepts OTLP/HTTP trace data from customer applications and forwards
/// it through the Agent365 Observability exporter pipeline.
/// </summary>
public static class ObservabilityEndpoints
{
    /// <summary>
    /// Maps the OTLP/HTTP receiver endpoints that accept traces from customer apps.
    /// </summary>
    public static WebApplication MapObservabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/observability").WithTags("Observability");

        // POST /api/v1/observability/v1/traces — OTLP/HTTP trace receiver (standard OTLP path)
        group.MapPost("/v1/traces", async (
            HttpContext context,
            OtlpTraceReceiver receiver,
            ILogger<OtlpTraceReceiver> logger) =>
        {
            var body = await ReadBodyAsync(context.Request);
            if (body.Length == 0)
            {
                return Results.BadRequest(new { error = "Empty request body" });
            }

            var contentType = context.Request.ContentType ?? "application/json";
            var result = await receiver.ReceiveTracesAsync(body, contentType, context.RequestAborted);

            return result.IsSuccess
                ? Results.Ok(new { acceptedSpans = result.SpanCount })
                : Results.StatusCode(result.StatusCode ?? 500);
        }).WithName("ReceiveTraces");

        // GET /api/v1/observability/config — Returns OTLP configuration for customer SDK
        group.MapGet("/config", (IOptions<SidecarOptions> options) =>
        {
            var sidecarOpts = options.Value;
            return Results.Ok(new OtlpConfigResponse
            {
                Endpoint = $"http://{sidecarOpts.Server.BindAddress}:{sidecarOpts.Server.Port}/api/v1/observability",
                Protocol = "http/json",
                Headers = new Dictionary<string, string>
                {
                    ["x-a365-agent-id"] = sidecarOpts.Agent.Id,
                    ["x-a365-tenant-id"] = sidecarOpts.Auth.TenantId,
                },
            });
        }).WithName("GetObservabilityConfig");

        return app;
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        // Handle gzip-compressed bodies (standard for OTLP)
        if (string.Equals(request.Headers.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
        {
            using var decompressed = new MemoryStream();
            await using (var gzip = new GZipStream(request.Body, CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(decompressed);
            }

            return decompressed.ToArray();
        }

        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}

/// <summary>
/// OTLP configuration response for customer SDKs.
/// </summary>
public sealed class OtlpConfigResponse
{
    /// <summary>
    /// OTLP endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// OTLP protocol (http/json or http/protobuf).
    /// </summary>
    public string Protocol { get; set; } = "http/json";

    /// <summary>
    /// Required headers for OTLP requests.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}
