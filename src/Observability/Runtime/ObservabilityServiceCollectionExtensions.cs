// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Extensions.DependencyInjection;
    using System;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agent 365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional configuration delegate for the Builder.</param>
        /// <param name="useOpenTelemetryBuilder">Whether to use the OpenTelemetryBuilder.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use.</param>
        /// <returns>The configured service collection.</returns>
        public static IServiceCollection AddTracing(
            this IServiceCollection services,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true,
            Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
        {
            var builder = new Builder(services: services, useOpenTelemetryBuilder: useOpenTelemetryBuilder, agent365ExporterType: agent365ExporterType);
            configure?.Invoke(builder);
            return builder.Build();
        }
    }
}
