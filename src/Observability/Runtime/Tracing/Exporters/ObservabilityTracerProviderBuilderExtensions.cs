// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Extension methods to add Agent365 Exporter to OpenTelemetry TracerProviderBuilder.
    /// </summary>
    public static class ObservabilityTracerProviderBuilderExtensions
    {
        private static readonly Lazy<ILoggerFactory> FallbackConsoleLoggerFactory = new Lazy<ILoggerFactory>(() => LoggerFactory.Create(b => b.AddConsole()));
        
        // Static guard to ensure Agent365 exporters are only registered once per process.
        // This prevents duplicate export processors when clients call AddAgent365Exporter multiple times via different builder extension paths or mixed API usage.
        internal static readonly ConcurrentDictionary<Agent365ExporterType, bool> RegisteredExporters = new ConcurrentDictionary<Agent365ExporterType, bool>();

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
            if (ObservabilityTracerProviderBuilderExtensions.RegisteredExporters.TryAdd(exporterType, true))
            {
                // Ensure required services are registered
                var exporterOptions = serviceProvider.GetRequiredService<Agent365ExporterOptions>();
                var httpClient = serviceProvider.GetService<HttpClient>();

                // Resolve ILoggerFactory from DI to ensure loggers have proper lifetime; fall back to NullLogger when unavailable.
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>() ?? ObservabilityTracerProviderBuilderExtensions.FallbackConsoleLoggerFactory.Value;
                var logger = serviceProvider.GetService<ILogger<Agent365Exporter>>() ?? loggerFactory.CreateLogger<Agent365Exporter>();
                var coreLogger = serviceProvider.GetService<ILogger<Agent365ExporterCore>>() ?? loggerFactory.CreateLogger<Agent365ExporterCore>();
                var formatterLogger = serviceProvider.GetService<ILogger<ExportFormatter>>() ?? loggerFactory.CreateLogger<ExportFormatter>();

                // Create ExportFormatter and Agent365ExporterCore
                var exportFormatter = new ExportFormatter(formatterLogger);
                var exporterCore = new Agent365ExporterCore(exportFormatter, coreLogger);

                switch (exporterType)
                {
                    case Agent365ExporterType.Agent365ExporterAsync:
                        builder.AddProcessor(new BatchActivityExportProcessorAsync(
                            new Agent365ExporterAsync(core: exporterCore, logger: logger, options: exporterOptions, resource: null, httpClient: httpClient),
                            maxQueueSize: exporterOptions.MaxQueueSize,
                            scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                            maxExportBatchSize: exporterOptions.MaxExportBatchSize));
                        break;

                    case Agent365ExporterType.Agent365Exporter:
                        builder.AddProcessor(new BatchActivityExportProcessor(
                            new Agent365Exporter(core: exporterCore, logger: logger, options: exporterOptions, resource: null, httpClient: httpClient),
                            maxQueueSize: exporterOptions.MaxQueueSize,
                            scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                            exporterTimeoutMilliseconds: exporterOptions.ExporterTimeoutMilliseconds,
                            maxExportBatchSize: exporterOptions.MaxExportBatchSize));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(exporterType), exporterType, "Unknown Agent365ExporterType specified.");
                }
            }

            return builder;
        }
    }
}