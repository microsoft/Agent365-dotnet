// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Async delegate used by the exporter to obtain an auth token for a specific agent + tenant.
    /// Must be fast and non-blocking (use internal caching elsewhere).
    /// Return null/empty to omit the Authorization header.
    /// </summary>
    public delegate Task<string?> AsyncAuthTokenResolver(string agentId, string tenantId);

    /// <summary>
    /// Delegate used by the exporter to resolve the island tenant domain for a given tenant id.
    /// </summary>
    public delegate string TenantDomainResolver(string tenantId);

    /// <summary>
    /// Configuration for Agent365Exporter.
    /// Only ClusterCategory and TokenResolver are required for core operation.
    /// </summary>
    public sealed class Agent365ExporterOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365ExporterOptions"/> class with default settings.
        /// </summary>
        /// <remarks>The default constructor sets the <c>DomainResolver</c> property to resolve tenant
        /// endpoints using the current <c>ClusterCategory</c> value.</remarks>
        public Agent365ExporterOptions()
        {
            this.DomainResolver = tenantId => new PowerPlatformApiDiscovery(this.ClusterCategory).GetTenantIslandClusterEndpoint(tenantId);
        }

        /// <summary>
        /// Cluster region argument. Defaults to production.
        /// </summary>
        public string ClusterCategory { get; set; } = "production";

        /// <summary>
        /// Async delegate used to resolve the auth token. REQUIRED.
        /// </summary>
        public AsyncAuthTokenResolver? TokenResolver { get; set; }

        /// <summary>
        /// Delegate used to resolve the island tenant domain for a given tenant id.
        /// </summary>
        public TenantDomainResolver DomainResolver { get; set; }

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
    }
}
