// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Minimal OTLP/HTTP JSON exporter for traces.
    /// Sends POST {Endpoint}/v1/traces with application/json.
    /// </summary>

    public sealed class Agent365Exporter : BaseExporter<Activity>
    {
        private readonly HttpClient _httpClient;
        private readonly Resource _resource;
        private readonly ILogger<Agent365Exporter> _logger;
        private readonly Agent365ExporterOptions _options;
        private readonly Agent365ExporterCore _core;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365Exporter"/> class.
        /// </summary>
        /// <param name="core">The Agent365ExporterCore instance.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">The exporter configuration options.</param>
        /// <param name="resource">Optional OpenTelemetry resource information.</param>
        /// <param name="httpClient">Optional HttpClient instance.</param>
        public Agent365Exporter(
            Agent365ExporterCore core,
            ILogger<Agent365Exporter> logger,
            Agent365ExporterOptions options,
            Resource? resource = null,
            HttpClient? httpClient = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.TokenResolver == null)
                throw new ArgumentNullException(nameof(options.TokenResolver), "Agent365ExporterOptions.TokenResolver must be provided.");

            _httpClient = httpClient ?? new HttpClient();
            _resource = resource ?? ResourceBuilder.CreateEmpty().Build();
        }

        /// <summary>
        /// Exports a batch of OpenTelemetry activities to the Microsoft Agent 365 observability platform.
        /// </summary>
        /// <param name="batch">The batch of activities to export.</param>
        /// <returns>The export result indicating success or failure.</returns>
        public override ExportResult Export(in Batch<Activity> batch)
        {
            _logger.LogInformation("Agent365Exporter: Exporting batch of {Count} spans.", batch.Count);

            try
            {
                var groups = _core.PartitionByIdentity(batch);
                if (groups.Count == 0)
                {
                    _logger.LogInformation("Agent365Exporter: No spans with tenant/agent identity found; nothing exported.");
                    return ExportResult.Success;
                }

                // Use the async core method, synchronously
                return _core.ExportBatchCoreAsync(
                    groups: groups,
                    resource: _resource,
                    options: _options,
                    tokenResolver: (agentId, tenantId) => _options.TokenResolver!(agentId, tenantId),
                    sendAsync: request => _httpClient.SendAsync(request)
                ).GetAwaiter().GetResult();
            }
            catch (Exception exOuter)
            {
                _logger.LogError(exOuter, "Agent365Exporter: Unhandled export exception.");
                return ExportResult.Failure;
            }
        }
    }
}

