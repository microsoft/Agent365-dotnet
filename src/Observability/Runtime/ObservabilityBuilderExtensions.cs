// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;

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
            if (!builder.Services.Any(s => s.ServiceType == typeof(Builder)))
            {
                var localbuilder = new Builder(
                        services: builder.Services!,
                        useOpenTelemetryBuilder: useOpenTelemetryBuilder,
                        agent365ExporterType: agent365ExporterType,
                        configuration: builder.Configuration);
                configure?.Invoke(localbuilder);
                localbuilder.Build();
                builder.Services.AddSingleton<Builder>(localbuilder);
            }
            else
            {
                Console.WriteLine("A365 tracing has already been configured. Duplicate call to AddA365Tracing() will be ignored.");
            }

            return builder;
        }
    }
}