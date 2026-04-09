// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    /// Delegate used by the exporter to resolve the endpoint host or URL for a given tenant id.
    /// The return value may be a bare host name (e.g. "agent365.svc.cloud.microsoft") or a full URL
    /// (e.g. "https://agent365.svc.cloud.microsoft").
    /// </summary>
    public delegate string TenantDomainResolver(string tenantId);

    /// <summary>
    /// Configuration for Agent365Exporter.
    /// Only TokenResolver is required for core operation.
    /// </summary>
    public sealed class Agent365ExporterOptions
    {
        /// <summary>
        /// The default endpoint host for Agent365 observability.
        /// </summary>
        public const string DefaultEndpointHost = "agent365.svc.cloud.microsoft";

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365ExporterOptions"/> class with default settings.
        /// </summary>
        /// <remarks>The default constructor sets the <c>DomainResolver</c> property to return the default
        /// Agent365 endpoint host (<c>agent365.svc.cloud.microsoft</c>).</remarks>
        public Agent365ExporterOptions()
        {
            this.DomainResolver = tenantId => DefaultEndpointHost;
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
        /// Delegate used to resolve the endpoint host or URL for a given tenant id.
        /// Defaults to returning <see cref="DefaultEndpointHost"/>.
        /// </summary>
        public TenantDomainResolver DomainResolver { get; set; }

        /// <summary>
        /// When true, uses the service-to-service (S2S) endpoint path: /observabilityService/tenants/{tenantId}/otlp/agents/{agentId}/traces
        /// When false (default), uses the standard endpoint path: /observability/tenants/{tenantId}/otlp/agents/{agentId}/traces
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
