// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Async delegate used by the exporter to obtain an app-only OBS auth token for a specific agent and tenant.
    /// Must be fast and non-blocking; cache tokens in the resolver and refresh only near expiry.
    /// Return null or empty to fail the export batch without sending a request.
    /// </summary>
    public delegate Task<string?> AsyncAuthTokenResolver(string agentId, string tenantId);

    /// <summary>
    /// Async delegate used by the exporter to obtain an app-only OBS auth token using rich context.
    /// Provides additional fields (e.g. <see cref="TokenResolverContext.Identity"/>)
    /// beyond what <see cref="AsyncAuthTokenResolver"/> offers.
    /// Must be fast and non-blocking; cache tokens in the resolver and refresh only near expiry.
    /// Return null or empty to fail the export batch without sending a request.
    /// </summary>
    public delegate Task<string?> AsyncContextualTokenResolver(TokenResolverContext context);

    /// <summary>
    /// Delegate used by the exporter to resolve the endpoint host or URL for a given tenant id.
    /// The return value may be a bare host name (e.g. "agent365.svc.cloud.microsoft") or a full URL
    /// (e.g. "https://agent365.svc.cloud.microsoft").
    /// </summary>
    public delegate string TenantDomainResolver(string tenantId);

    /// <summary>
    /// Configuration for Agent365Exporter.
    /// A configured app-only token resolver is required for core operation.
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
        /// Async delegate used to resolve the app-only OBS auth token.
        /// Either this or <see cref="ContextualTokenResolver"/> must be set.
        /// When both are set, <see cref="ContextualTokenResolver"/> takes precedence.
        /// The exporter invokes this resolver once per tenant/agent identity group in each export batch, so a batch
        /// that contains several identities invokes it several times. It never falls back to a delegated token.
        /// </summary>
        public AsyncAuthTokenResolver? TokenResolver { get; set; }

        /// <summary>
        /// Async delegate used to resolve the app-only OBS auth token with rich context, which may include the
        /// agentic user ID associated with the export batch context for cache selection or diagnostics.
        /// Takes precedence over <see cref="TokenResolver"/> when set.
        /// The exporter does not guarantee separate batching or resolver invocation per agentic user ID.
        /// This resolver must not perform delegated, OBO, or user_fic authentication for OBS export.
        /// </summary>
        public AsyncContextualTokenResolver? ContextualTokenResolver { get; set; }

        /// <summary>
        /// Delegate used to resolve the endpoint host or URL for a given tenant id.
        /// Defaults to returning <see cref="DefaultEndpointHost"/>.
        /// </summary>
        public TenantDomainResolver DomainResolver { get; set; }

        /// <summary>
        /// OBS export always uses the service-to-service (S2S) OTLP endpoint path:
        /// /observabilityService/tenants/{tenantId}/otlp/agents/{agentId}/traces.
        /// This compatibility switch is ignored even when set to <c>false</c>.
        /// </summary>
        [Obsolete("Agent 365 OBS export always uses the service-to-service /observabilityService OTLP endpoint; this option is ignored.", false)]
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
        /// Upper bound on HTTP request body size in bytes used by per-request payload chunking.
        /// 100 KB headroom under the 1 MB server limit accounts for estimator error and JSON/envelope
        /// overhead (for example, resource attributes and scope wrappers).
        /// Default is 900,000 bytes.
        /// </summary>
        public long MaxPayloadBytes { get; set; } = 900_000;
    }
}
