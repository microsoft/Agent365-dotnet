// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability;

using Microsoft.Agents.A365.Observability.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Agents.A365.Observability.Caching;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

/// <summary>
/// Provides extension methods for configuring Kairo SDK with OpenTelemetry tracing.
/// </summary>
public static class KairoSdk
{
    /// <summary>
    /// Adds the Kairo SDK with OpenTelemetry tracing for AI agents and tools.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional configuration delegate for the Builder.</param>
    /// <param name="sentinelConfiguration">Optional configuration for integrating Sentinel capabilities.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddTracing(
        this IServiceCollection services,
        Action<Builder>? configure = null,
        SentinelConfiguration? sentinelConfiguration = null)
    {
        var builder = new Builder(services);
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Adds agentic token handling to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="clusterCategory">The cluster category for the Kairo exporter options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAgenticTracingExporter(this IServiceCollection services, string clusterCategory = "production")
    {
        services.AddSingleton<IExporterTokenCache<AgenticTokenStruct>, AgenticTokenCache>();

        services.AddSingleton(sp =>
        {
            var cache = sp.GetRequiredService<IExporterTokenCache<AgenticTokenStruct>>();
            return new Agent365ExporterOptions
            {
                ClusterCategory = clusterCategory,
                TokenResolver = (agentId, tenantId) => cache.GetObservabilityToken(agentId, tenantId)
            };
        });

        return services;
    }

    /// <summary>
    /// Adds a service tracing exporter to the service collection.
    /// Uses the service-to-service (S2S) endpoint for trace exports.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="clusterCategory">The cluster category for the Kairo exporter options.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddServiceTracingExporter(this IServiceCollection services, string clusterCategory = "production")
    {
        services.AddSingleton<IExporterTokenCache<string>, ServiceTokenCache>();

        services.AddSingleton(sp =>
        {
            var cache = sp.GetRequiredService<IExporterTokenCache<string>>();
            return new Agent365ExporterOptions
            {
                ClusterCategory = clusterCategory,
                TokenResolver = (agentId, tenantId) => cache.GetObservabilityToken(agentId, tenantId),
                UseS2SEndpoint = true // Service-to-service uses S2S endpoint
            };
        });

        return services;
    }
}
