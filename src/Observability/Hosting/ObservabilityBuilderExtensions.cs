// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Hosting
{
    using System;
    using Microsoft.Agents.A365.Observability.Runtime;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agent 365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityBuilderExtensions
    {
        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="webHostBuilder">The web host builder to which tracing services will be added.</param>
        /// <param name="configure">An optional delegate to further configure the tracing builder.</param>
        /// <param name="useOpenTelemetryBuilder">Specifies whether to use the OpenTelemetry builder for configuration. Defaults to <c>true</c>.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use for tracing. Defaults to <see cref="Agent365ExporterType.Agent365Exporter"/>.</param>
        /// <returns>The original <see cref="IWebHostBuilder"/> instance with tracing configured.</returns>
        public static IWebHostBuilder AddA365Tracing(
            this IWebHostBuilder webHostBuilder,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true,
            Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
        {
            webHostBuilder.ConfigureServices((context, services) =>
            {
                var localBuilder = new Builder(
                    services: services,
                    useOpenTelemetryBuilder: useOpenTelemetryBuilder,
                    agent365ExporterType: agent365ExporterType,
                    configuration: context.Configuration);
                configure?.Invoke(localBuilder);
                localBuilder.Build();
            });
            return webHostBuilder;
        }

        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="builder">The generic host builder to which tracing services will be added.</param>
        /// <param name="configure">An optional delegate to further configure the tracing builder.</param>
        /// <param name="useOpenTelemetryBuilder">Specifies whether to use the OpenTelemetry builder for configuration. Defaults to <c>true</c>.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use for tracing. Defaults to <see cref="Agent365ExporterType.Agent365Exporter"/>.</param>
        /// <returns>The original <see cref="IHostBuilder"/> instance with tracing configured.</returns>
        public static IHostBuilder AddA365Tracing(
            this IHostBuilder builder,
            Action<Builder>? configure = null,
            bool useOpenTelemetryBuilder = true,
            Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter)
        {
            builder.ConfigureServices((context, services) =>
            {
                var localBuilder = new Builder(
                    services: services,
                    useOpenTelemetryBuilder: useOpenTelemetryBuilder,
                    agent365ExporterType: agent365ExporterType,
                    configuration: context.Configuration);
                configure?.Invoke(localBuilder);
                localBuilder.Build();
            });
            return builder;
        }
    }
}
