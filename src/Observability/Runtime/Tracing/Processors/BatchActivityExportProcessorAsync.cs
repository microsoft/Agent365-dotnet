// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;
using OpenTelemetry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    /// <summary>
    /// Implements an async processor that batches <see cref="Activity"/> objects before calling exporter asynchronously.
    /// </summary>
    public class BatchActivityExportProcessorAsync : BaseProcessor<Activity>
    {
        internal const int DefaultMaxQueueSize = 2048;
        internal const int DefaultScheduledDelayMilliseconds = 5000;
        internal const int DefaultMaxExportBatchSize = 512;

        private readonly BaseExporterAsync<Activity> exporter;
        private readonly int maxQueueSize;
        private readonly int scheduledDelayMilliseconds;
        private readonly int maxExportBatchSize;

        private readonly ConcurrentQueue<Activity> queue;
        private readonly SemaphoreSlim signal;
        private readonly CancellationTokenSource shutdownCts;
        private Task? workerTask;
        private bool disposed;
        private readonly string friendlyTypeName;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchActivityExportProcessorAsync"/> class.
        /// </summary>
        /// <param name="exporter">The async exporter instance.</param>
        /// <param name="maxQueueSize">Maximum queue size.</param>
        /// <param name="scheduledDelayMilliseconds">Delay between exports in ms.</param>
        /// <param name="maxExportBatchSize">Max batch size per export.</param>
        public BatchActivityExportProcessorAsync(
            BaseExporterAsync<Activity> exporter,
            int maxQueueSize = DefaultMaxQueueSize,
            int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
            int maxExportBatchSize = DefaultMaxExportBatchSize)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.maxQueueSize = maxQueueSize;
            this.scheduledDelayMilliseconds = scheduledDelayMilliseconds;
            this.maxExportBatchSize = maxExportBatchSize;

            this.queue = new ConcurrentQueue<Activity>();
            this.signal = new SemaphoreSlim(0);
            this.shutdownCts = new CancellationTokenSource();
            this.workerTask = Task.Run(ProcessLoopAsync);
            this.friendlyTypeName = $"{this.GetType().Name}{{{exporter.GetType().Name}}}";
        }

        /// <summary>
        /// Called when an <see cref="Activity"/> is ended.
        /// </summary>
        /// <param name="data">The activity to export.</param>
        public override void OnEnd(Activity data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!data.Recorded)
            {
                return;
            }

            this.OnExport(data);
        }

        /// <summary>
        /// Enqueues activity data for export. If the queue is full, the data is dropped.
        /// </summary>
        /// <param name="data">The activity to export.</param>
        private void OnExport(Activity data)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BatchActivityExportProcessorAsync));

            if (queue.Count < maxQueueSize)
            {
                queue.Enqueue(data);
                signal.Release();
            }
            // else: drop, could count dropped
        }

        /// <summary>
        /// Forces the processor to flush all queued activities asynchronously.
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
            await exporter.ForceFlushAsync(cancellationToken);
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
        /// The main processing loop that batches and exports activities asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous processing loop.</returns>
        private async Task ProcessLoopAsync()
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

                    var batch = new List<Activity>(maxExportBatchSize);
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
        /// Returns a string that represents the current processor.
        /// </summary>
        /// <returns>
        /// A string containing the friendly type name of the processor and exporter.
        /// </returns>
        public override string ToString()
            => this.friendlyTypeName;

        /// <summary>
        /// Releases resources used by the processor and exporter.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    shutdownCts.Cancel();
                    signal.Dispose();
                    shutdownCts.Dispose();
                    try
                    {
                        exporter.Dispose();
                    }
                    catch (Exception)
                    {
                        // handle/log as needed
                    }
                }
                disposed = true;
            }
        }
    }
}
