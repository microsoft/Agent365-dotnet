// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    /// <summary>
    /// Implements processor that batches telemetry objects before calling exporter asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class BatchExportProcessorAsync<T> : BaseExportProcessorAsync<T>
        where T : class
    {
        /// <summary>
        /// The default maximum number of telemetry objects allowed in the queue.
        /// </summary>
        internal const int DefaultMaxQueueSize = 2048;

        /// <summary>
        /// The default delay in milliseconds between scheduled export attempts.
        /// </summary>
        internal const int DefaultScheduledDelayMilliseconds = 5000;

        /// <summary>
        /// The default maximum number of telemetry objects to export in a single batch.
        /// </summary>
        internal const int DefaultMaxExportBatchSize = 512;

        private readonly int maxQueueSize;
        private readonly int scheduledDelayMilliseconds;
        private readonly int maxExportBatchSize;

        private readonly ConcurrentQueue<T> queue;
        private readonly SemaphoreSlim signal;
        private readonly CancellationTokenSource shutdownCts;
        private Task? workerTask;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchExportProcessorAsync{T}"/> class.
        /// </summary>
        /// <param name="exporter">The exporter used to export telemetry objects.</param>
        /// <param name="maxQueueSize">The maximum number of telemetry objects allowed in the queue.</param>
        /// <param name="scheduledDelayMilliseconds">The delay in milliseconds between scheduled export attempts.</param>
        /// <param name="maxExportBatchSize">The maximum number of telemetry objects to export in a single batch.</param>
        protected BatchExportProcessorAsync(
            BaseExporterAsync<T> exporter,
            int maxQueueSize = DefaultMaxQueueSize,
            int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
            int maxExportBatchSize = DefaultMaxExportBatchSize)
            : base(exporter)
        {
            this.maxQueueSize = maxQueueSize;
            this.scheduledDelayMilliseconds = scheduledDelayMilliseconds;
            this.maxExportBatchSize = maxExportBatchSize;

            this.queue = new ConcurrentQueue<T>();
            this.signal = new SemaphoreSlim(0);
            this.shutdownCts = new CancellationTokenSource();
            this.workerTask = Task.Run(ProcessLoopAsync);
        }

        /// <summary>
        /// Enqueues telemetry data for export. If the queue is full, the data is dropped.
        /// </summary>
        /// <param name="data">The telemetry object to export.</param>
        protected override void OnExport(T data)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BatchExportProcessorAsync<T>));

            if (queue.Count < maxQueueSize)
            {
                queue.Enqueue(data);
                signal.Release();
            }
            // else: drop, could count dropped
        }

        /// <summary>
        /// Forces the processor to flush all queued telemetry objects asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous flush operation.</returns>
        public async Task ForceFlushAsync(CancellationToken cancellationToken = default)
        {
            signal.Release();
            var sw = Stopwatch.StartNew();
            while (!queue.IsEmpty)
            {
                if (cancellationToken.IsCancellationRequested) break;
                await Task.Delay(100, cancellationToken);
            }
        }

        /// <summary>
        /// Shuts down the processor and exporter asynchronously, releasing any resources if necessary.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous shutdown operation.</returns>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            shutdownCts.Cancel();
            signal.Release();

            if (workerTask != null)
            {
                await workerTask;
            }

            await exporter.ShutdownAsync(cancellationToken);
        }

        /// <summary>
        /// The main processing loop that batches and exports telemetry objects asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing loop.</returns>
        protected virtual async Task ProcessLoopAsync()
        {
            var token = shutdownCts.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.WhenAny(
                        Task.Delay(scheduledDelayMilliseconds, token),
                        signal.WaitAsync(token));

                    if (queue.IsEmpty) continue;

                    var batch = new List<T>(maxExportBatchSize);
                    while (batch.Count < maxExportBatchSize && queue.TryDequeue(out var item))
                    {
                        batch.Add(item);
                    }

                    if (batch.Count > 0)
                    {
                        await exporter.ExportAsync(batch, token);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Releases resources used by the processor and exporter.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                shutdownCts.Cancel();
                signal.Dispose();
                shutdownCts.Dispose();
                disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}