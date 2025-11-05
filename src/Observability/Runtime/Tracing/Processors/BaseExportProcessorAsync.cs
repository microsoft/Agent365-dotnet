// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using OpenTelemetry;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    /// <summary>
    /// Implements processor that exports telemetry objects asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class BaseExportProcessorAsync<T> : BaseProcessor<T>
        where T : class
    {
        /// <summary>
        /// Gets the exporter used by the processor.
        /// </summary>
        protected readonly BaseExporterAsync<T> exporter;

        private readonly string friendlyTypeName;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExportProcessorAsync{T}"/> class.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        protected BaseExportProcessorAsync(BaseExporterAsync<T> exporter)
        {
            this.friendlyTypeName = $"{this.GetType().Name}{{{exporter.GetType().Name}}}";
            this.exporter = exporter;
        }

        /// <summary>
        /// Gets the exporter instance used by this processor.
        /// </summary>
        internal BaseExporterAsync<T> Exporter => this.exporter;

        /// <summary>
        /// Returns a string that represents the current processor.
        /// </summary>
        /// <returns>
        /// A string containing the friendly type name of the processor and exporter.
        /// </returns>
        public override string ToString()
            => this.friendlyTypeName;

        /// <summary>
        /// Called when a telemetry object is started.
        /// </summary>
        /// <param name="data">The telemetry object being started.</param>
        public sealed override void OnStart(T data)
        {
        }

        /// <summary>
        /// Called when a telemetry object is ended.
        /// </summary>
        /// <param name="data">The telemetry object being ended.</param>
        public override void OnEnd(T data)
        {
            this.OnExport(data);
        }

        /// <summary>
        /// Called synchronously when a telemetry object is exported.
        /// </summary>
        /// <param name="data">The exported telemetry object.</param>
        /// <remarks>
        /// This function should be thread-safe and not block indefinitely or throw exceptions.
        /// </remarks>
        protected abstract void OnExport(T data);

        /// <summary>
        /// Forces the processor and exporter to flush any buffered telemetry data.
        /// </summary>
        /// <param name="timeoutMilliseconds">The maximum time to wait for the flush operation, in milliseconds.</param>
        /// <returns>
        /// <c>true</c> if the flush completed successfully within the timeout; otherwise, <c>false</c>.
        /// </returns>
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            // Async flush, but block for completion up to the timeout
            return this.exporter.ForceFlushAsync().Wait(timeoutMilliseconds);
        }

        /// <summary>
        /// Shuts down the processor and exporter, releasing any resources if necessary.
        /// </summary>
        /// <param name="timeoutMilliseconds">The maximum time to wait for the shutdown operation, in milliseconds.</param>
        /// <returns>
        /// <c>true</c> if the shutdown completed successfully within the timeout; otherwise, <c>false</c>.
        /// </returns>
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            // Async shutdown, but block for completion up to the timeout
            return this.exporter.ShutdownAsync().Wait(timeoutMilliseconds);
        }

        /// <summary>
        /// Releases resources used by the processor and exporter.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    try
                    {
                        this.exporter.Dispose();
                    }
                    catch (Exception)
                    {
                        // handle/log as needed
                        // OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
