// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Async delegate used by the exporter to obtain an auth token for a specific agent + tenant.
    /// Must be fast and non-blocking (use internal caching elsewhere).
    /// Return null/empty to omit the Authorization header.
    /// </summary>
    public delegate Task<string?> AsyncAuthTokenResolver(string agentId, string tenantId);

    /// <summary>
    /// Configuration for Agent365Exporter.
    /// Only ClusterCategory and TokenResolver are required for core operation.
    /// </summary>
    public sealed class Agent365ExporterOptions
    {
        /// <summary>
        /// Cluster region argument. Defaults to production.
        /// </summary>
        public string ClusterCategory { get; set; } = "production";

        /// <summary>
        /// Async delegate used to resolve the auth token. REQUIRED.
        /// </summary>
        public AsyncAuthTokenResolver? TokenResolver { get; set; }

        /// <summary>
        /// When true, uses the service-to-service (S2S) endpoint path: /maven/agent365/service/agents/{agentId}/traces
        /// When false (default), uses the standard endpoint path: /maven/agent365/agents/{agentId}/traces
        /// Default is false.
        /// </summary>
        public bool UseS2SEndpoint { get; set; } = false;

        /// <summary>
        /// Maximum queue size for the batch processor.
        /// Default is 2048.
        /// </summary>
        public int MaxQueueSize { get; set; } = 2048;

        /// <summary>
        /// Delay in milliseconds between export batches.
        /// Default is 5000 (5 seconds).
        /// </summary>
        public int ScheduledDelayMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Timeout in milliseconds for the export operation.
        /// Default is 30000 (30 seconds).
        /// </summary>
        public int ExporterTimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Maximum batch size for export operations.
        /// Default is 512.
        /// </summary>
        public int MaxExportBatchSize { get; set; } = 512;

        /// <summary>
        /// When true the exporter targets the custom Agent365 domain instead of the PPAPI gateway derived tenant island endpoint.
        /// Default is false to preserve existing behavior until the custom domain is fully adopted.
        /// </summary>
        public bool UseCustomDomain { get; set; } = false;
    }
}
