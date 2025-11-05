// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Extension methods to add Agent365 Exporter to OpenTelemetry TracerProviderBuilder.
    /// </summary>
    public static class ObservabilityTracerProviderBuilderExtensions
    {
        /// <summary>
        /// Adds the Agent365 Exporter to the OpenTelemetry TracerProviderBuilder using deferred initialization.
        /// </summary>
        /// <param name="builder">The TracerProviderBuilder to configure.</param>
        /// <param name="exporterType">The Agent365 exporter type to use.</param>
        public static TracerProviderBuilder AddAgent365Exporter(this TracerProviderBuilder builder, Agent365ExporterType exporterType = Agent365ExporterType.Agent365Exporter)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var deferredBuilder = builder as IDeferredTracerProviderBuilder;
            if (deferredBuilder == null)
            {
                throw new InvalidOperationException("The provided TracerProviderBuilder does not implement IDeferredTracerProviderBuilder.");
            }

            return deferredBuilder.Configure((sp, builder) => ObservabilityTracerProviderBuilderExtensions.ConfigureInternal(sp, builder, exporterType));
        }

        /// <summary>
        /// Adds the Agent365 Exporter to the OpenTelemetry TracerProviderBuilder using the provided service collection.
        /// </summary>
        /// <param name="builder">The TracerProviderBuilder to configure.</param>
        /// <param name="serviceCollection">The service collection to use for dependency injection.</param>
        /// <param name="exporterType">The Agent365 exporter type to use.</param>
        public static TracerProviderBuilder AddAgent365Exporter(this TracerProviderBuilder builder, IServiceCollection serviceCollection, Agent365ExporterType exporterType = Agent365ExporterType.Agent365Exporter)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            return ObservabilityTracerProviderBuilderExtensions.ConfigureInternal(
                serviceProvider: serviceCollection.BuildServiceProvider(),
                builder: builder,
                exporterType: exporterType);
        }

        private static TracerProviderBuilder ConfigureInternal(IServiceProvider serviceProvider, TracerProviderBuilder builder, Agent365ExporterType exporterType)
        {
            var exporterOptions = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
            var logger = null as ILogger<Agent365Exporter>;
            try
            {
                logger = serviceProvider.GetRequiredService<ILogger<Agent365Exporter>>();
            }
            catch
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                logger = loggerFactory.CreateLogger<Agent365Exporter>();
            }

            switch (exporterType)
            {
                case Agent365ExporterType.Agent365ExporterAsync:
                    builder.AddProcessor(new BatchActivityExportProcessorAsync(
                        new Agent365ExporterAsync(logger, exporterOptions),
                        maxQueueSize: exporterOptions.MaxQueueSize,
                        scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                        maxExportBatchSize: exporterOptions.MaxExportBatchSize));
                    break;

                case Agent365ExporterType.Agent365Exporter:
                    builder.AddProcessor(new BatchActivityExportProcessor(
                        new Agent365Exporter(options: exporterOptions, resource: null, logger: logger),
                        maxQueueSize: exporterOptions.MaxQueueSize,
                        scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                        exporterTimeoutMilliseconds: exporterOptions.ExporterTimeoutMilliseconds,
                        maxExportBatchSize: exporterOptions.MaxExportBatchSize));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(exporterType), exporterType, "Unknown Agent365ExporterType specified.");
            }
            return builder;
        }
    }
}