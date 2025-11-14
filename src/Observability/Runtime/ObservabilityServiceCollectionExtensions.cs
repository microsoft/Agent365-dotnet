// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Runtime
{
    using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System;

    /// <summary>
    /// Provides extension methods for configuring Microsoft Agent 365 SDK with OpenTelemetry tracing.
    /// </summary>
    public static class ObservabilityServiceCollectionExtensions
    {

        /// <summary>
        /// Adds the Microsoft Agent 365 SDK with OpenTelemetry tracing for AI agents and tools.
        /// </summary>
        /// <typeparam name="TBuilder"></typeparam>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <param name="useOpenTelemetryBuilder"></param>
        /// <param name="agent365ExporterType"></param>
        /// <returns></returns>
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

    }
}