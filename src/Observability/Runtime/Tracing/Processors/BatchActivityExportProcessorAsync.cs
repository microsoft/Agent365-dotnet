// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Processors
{
    /// <summary>
    /// Implements an async processor that batches <see cref="Activity"/> objects before calling exporter asynchronously.
    /// </summary>
    public class BatchActivityExportProcessorAsync : BatchExportProcessorAsync<Activity>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchActivityExportProcessorAsync"/> class.
        /// </summary>
        /// <param name="exporter">The async exporter instance.</param>
        /// <param name="maxQueueSize">Maximum queue size.</param>
        /// <param name="scheduledDelayMilliseconds">Delay between exports in ms.</param>
        /// <param name="maxExportBatchSize">Max batch size per export.</param>
        public BatchActivityExportProcessorAsync(
            BaseExporterAsync<Activity> exporter,
            int maxQueueSize = 2048,
            int scheduledDelayMilliseconds = 5000,
            int maxExportBatchSize = 512)
            : base(
                exporter,
                maxQueueSize,
                scheduledDelayMilliseconds,
                maxExportBatchSize)
        {
        }

        /// <inheritdoc />
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
    }
}
