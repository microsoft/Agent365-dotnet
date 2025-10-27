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
        public static TracerProviderBuilder AddAgent365Exporter(this TracerProviderBuilder builder)
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

            return deferredBuilder.Configure((sp, builder) => ObservabilityTracerProviderBuilderExtensions.ConfigureInternal(sp, builder));
        }

        /// <summary>
        /// Adds the Agent365 Exporter to the OpenTelemetry TracerProviderBuilder using the provided service collection.
        /// </summary>
        public static TracerProviderBuilder AddAgent365Exporter(this TracerProviderBuilder builder, IServiceCollection serviceCollection)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            return ObservabilityTracerProviderBuilderExtensions.ConfigureInternal(serviceProvider: serviceCollection.BuildServiceProvider(), builder: builder);
        }

        private static TracerProviderBuilder ConfigureInternal(IServiceProvider serviceProvider, TracerProviderBuilder builder)
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
            builder.AddProcessor(new BatchActivityExportProcessor(
                new Agent365Exporter(options: exporterOptions, resource: null, logger: logger),
                maxQueueSize: exporterOptions.MaxQueueSize,
                scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                exporterTimeoutMilliseconds: exporterOptions.ExporterTimeoutMilliseconds,
                maxExportBatchSize: exporterOptions.MaxExportBatchSize));
            return builder;
        }
    }
}