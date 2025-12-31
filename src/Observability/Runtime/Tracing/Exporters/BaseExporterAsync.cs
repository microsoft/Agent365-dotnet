// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Abstract base class for asynchronous exporters of telemetry objects.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to export.</typeparam>
    public abstract class BaseExporterAsync<T> : IDisposable
        where T : class
    {
        /// <summary>
        /// Exports a batch of telemetry objects asynchronously.
        /// </summary>
        /// <param name="batch">The batch of telemetry objects to export.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous export operation.</returns>
        public abstract Task ExportAsync(IReadOnlyCollection<T> batch, CancellationToken cancellationToken);

        /// <summary>
        /// Flushes any buffered telemetry data asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="Task{Boolean}"/> representing the asynchronous flush operation.
        /// Returns <c>true</c> if flush was successful or not required.
        /// </returns>
        public virtual Task<bool> ForceFlushAsync(CancellationToken cancellationToken = default)
        {
            // Default: nothing to flush
            return Task.FromResult(true);
        }

        /// <summary>
        /// Shuts down the exporter asynchronously, releasing any resources if necessary.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>
        /// A <see cref="Task{Boolean}"/> representing the asynchronous shutdown operation.
        /// Returns <c>true</c> if shutdown was successful or not required.
        /// </returns>
        public virtual Task<bool> ShutdownAsync(CancellationToken cancellationToken = default)
        {
            // Default: nothing to shutdown
            return Task.FromResult(true);
        }

        /// <summary>
        /// Releases resources used by the exporter.
        /// </summary>
        public virtual void Dispose()
        {
            // Default: nothing to dispose
        }
    }
}
