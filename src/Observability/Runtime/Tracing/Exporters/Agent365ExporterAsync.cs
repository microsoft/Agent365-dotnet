// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Minimal OTLP/HTTP JSON exporter for traces.
    /// Sends POST {Endpoint}/v1/traces with application/json.
    /// </summary>

    public sealed class Agent365ExporterAsync : BaseExporterAsync<Activity>
    {
        private readonly HttpClient _httpClient;
        private readonly Resource _resource;
        private readonly ILogger<Agent365Exporter> _logger;
        private readonly Agent365ExporterOptions _options;
        private readonly Agent365ExporterCore _core;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365ExporterAsync"/> class.
        /// </summary>
        /// <param name="core">The Agent365ExporterCore instance.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">The exporter configuration options.</param>
        /// <param name="resource">Optional OpenTelemetry resource information.</param>
        /// <param name="httpClient">Optional HttpClient instance.</param>
        public Agent365ExporterAsync(
            Agent365ExporterCore core,
            ILogger<Agent365Exporter> logger,
            Agent365ExporterOptions options,
            Resource? resource = null,
            HttpClient? httpClient = null)
        {
            this._core = core ?? throw new ArgumentNullException(nameof(core));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.TokenResolver == null && _options.ContextualTokenResolver == null)
                throw new ArgumentNullException(nameof(options.TokenResolver), "Agent365ExporterOptions.TokenResolver or ContextualTokenResolver must be provided.");

            this._httpClient = httpClient ?? HttpClientFactory.CreateWithTimeout(options.ExporterTimeoutMilliseconds);
            this._resource = resource ?? ResourceBuilder.CreateEmpty().Build();
        }

        /// <summary>
        /// Exports a batch of OpenTelemetry activities to the Microsoft Agent 365 observability platform asynchronously.
        /// </summary>
        /// <param name="batch">The batch of activities to export.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous export operation.</returns>
        public override async Task ExportAsync(IReadOnlyCollection<Activity> batch, CancellationToken cancellationToken)
        {
            this._logger.LogDebug("Agent365ExporterAsync: Exporting batch of {Count} spans.", batch.Count);

            try
            {
                var groups = _core.PartitionByIdentity(batch);
                if (groups.Count == 0)
                {
                    this._logger.LogDebug("Agent365ExporterAsync: No spans with tenant/agent identity found; nothing exported.");
                    return;
                }

                await _core.ExportBatchCoreAsync(
                    groups: groups,
                    resource: this._resource,
                    options: this._options,
                    tokenResolver: (agentId, tenantId, ct) => this._options.TokenResolver != null
                        ? this._options.TokenResolver(agentId, tenantId, ct)
                        : Task.FromResult<string?>(null),
                    sendAsync: request => this._httpClient.SendAsync(request, cancellationToken),
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                this._logger.LogWarning("Agent365ExporterAsync: Export operation was canceled.");
                throw;
            }
            catch (Exception exOuter)
            {
                this._logger.LogError(exOuter, "Agent365ExporterAsync: Unhandled export exception.");
            }
        }
    }
}

