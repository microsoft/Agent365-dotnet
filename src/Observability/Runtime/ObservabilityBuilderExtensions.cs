// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agent 365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityBuilderExtensions
    {

        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <typeparam name="TBuilder">The type of the application builder implementing <see cref="IHostApplicationBuilder"/>.</typeparam>
        /// <param name="builder">The application builder to which tracing services will be added.</param>
        /// <param name="configure">An optional delegate to further configure the tracing builder.</param>
        /// <param name="useOpenTelemetryBuilder">Specifies whether to use the OpenTelemetry builder for configuration. Defaults to <c>true</c>.</param>
        /// <param name="agent365ExporterType">The type of Agent 365 exporter to use for tracing. Defaults to <see cref="Agent365ExporterType.Agent365Exporter"/>.</param>
        /// <returns>The original <typeparamref name="TBuilder"/> instance with tracing configured.</returns>
        public static TBuilder AddA365Tracing<TBuilder>(
                this TBuilder builder,
                Action<Builder>? configure = null,
                bool useOpenTelemetryBuilder = true,
                Agent365ExporterType agent365ExporterType = Agent365ExporterType.Agent365Exporter) where TBuilder : IHostApplicationBuilder
        {
            var localbuilder = new Builder(services: builder.Services!, useOpenTelemetryBuilder: useOpenTelemetryBuilder, agent365ExporterType: agent365ExporterType,configuration: builder.Configuration);
            configure?.Invoke(localbuilder);
            localbuilder.Build();
            return builder;
        }


        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <param name="webHostBuilder"></param>
        /// <param name="configure"></param>
        /// <param name="useOpenTelemetryBuilder"></param>
        /// <param name="agent365ExporterType"></param>
        /// <returns></returns>
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
    }
}