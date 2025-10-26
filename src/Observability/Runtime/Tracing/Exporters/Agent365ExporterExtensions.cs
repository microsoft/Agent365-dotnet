using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
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
    public static class Agent365ExporterExtensions
    {
        /// <summary>
        /// Adds the Agent365 Exporter to the OpenTelemetry TracerProviderBuilder.
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

            return deferredBuilder.Configure((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<Agent365ExporterOptions>();
                var logger = sp.GetRequiredService<ILogger<Agent365Exporter>>();

                builder.AddProcessor(new BatchActivityExportProcessor(
                        new Agent365Exporter(logger, exporterOptions),
                        maxQueueSize: exporterOptions.MaxQueueSize,
                        scheduledDelayMilliseconds: exporterOptions.ScheduledDelayMilliseconds,
                        exporterTimeoutMilliseconds: exporterOptions.ExporterTimeoutMilliseconds,
                        maxExportBatchSize: exporterOptions.MaxExportBatchSize));
            });
        }
    }
}
