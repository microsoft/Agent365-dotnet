// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Utility methods for Agent365 trace exporters.
    /// Provides helpers for partitioning activities and building endpoint URIs.
    /// </summary>
    public class Agent365ExporterCore
    {
        private const string CorrelationIdHeaderKey = "x-ms-correlation-id";
        private readonly ExportFormatter _formatter;
        private readonly ILogger<Agent365ExporterCore> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365ExporterCore"/> class.
        /// </summary>
        /// <param name="formatter">The formatter instance used to format export payloads.</param>
        /// <param name="logger">The logger instance used to log messages during the export process.</param>
        public Agent365ExporterCore(ExportFormatter formatter, ILogger<Agent365ExporterCore> logger)
        {
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _logger = logger ?? NullLogger<Agent365ExporterCore>.Instance;
        }

        /// <summary>
        /// Partitions a batch of activities by tenant and agent identity.
        /// </summary>
        /// <param name="batch">The collection of activities to partition.</param>
        /// <returns>
        /// A list of tuples containing TenantId, AgentId, and the corresponding activities.
        /// </returns>
        public List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(IEnumerable<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();

            foreach (var activity in batch)
            {
                Agent365ExporterCore.TryAddActivityToMap(activity, map);
            }

            return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
        }

        /// <summary>
        /// Partitions a batch of activities by tenant and agent identity.
        /// </summary>
        /// <param name="batch">The collection of activities to partition.</param>
        /// <returns>
        /// A list of tuples containing TenantId, AgentId, and the corresponding activities.
        /// </returns>
        public List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(in Batch<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();

            foreach (var activity in batch)
            {
                Agent365ExporterCore.TryAddActivityToMap(activity, map);
            }

            return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
        }

        /// <summary>
        /// Builds the endpoint path for the trace export request based on tenant ID, agent ID and S2S setting.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="useS2SEndpoint">Whether to use the S2S endpoint.</param>
        /// <returns>The endpoint path string.</returns>
        public string BuildEndpointPath(string tenantId, string agentId, bool useS2SEndpoint)
        {
            var encodedTenantId = Uri.EscapeDataString(tenantId);
            var encodedAgentId = Uri.EscapeDataString(agentId);

            return useS2SEndpoint
                ? $"/observabilityService/tenants/{encodedTenantId}/agents/{encodedAgentId}/traces"
                : $"/observability/tenants/{encodedTenantId}/agents/{encodedAgentId}/traces";
        }

        /// <summary>
        /// Builds the full request URI for the trace export request.
        /// If the endpoint already includes a scheme (https://), it is used as-is.
        /// Otherwise, https:// is prepended. Plaintext http:// is not supported.
        /// </summary>
        /// <param name="endpoint">The base endpoint (domain or full HTTPS URL).</param>
        /// <param name="endpointPath">The endpoint path.</param>
        /// <returns>The full request URI string.</returns>
        /// <exception cref="ArgumentException">Thrown when the endpoint uses an http:// (non-TLS) scheme.</exception>
        public string BuildRequestUri(string endpoint, string endpointPath)
        {
            var normalizedEndpoint = endpoint.TrimEnd('/');

            if (normalizedEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Plaintext HTTP endpoints are not supported. Use HTTPS to protect credentials in transit.", nameof(endpoint));
            }

            if (normalizedEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return $"{normalizedEndpoint}{endpointPath}?api-version=1";
            }

            return $"https://{normalizedEndpoint}{endpointPath}?api-version=1";
        }

        /// <summary>
        /// Exports a batch of activities grouped by tenant and agent identity.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="resource"></param>
        /// <param name="options"></param>
        /// <param name="tokenResolver"></param>
        /// <param name="sendAsync"></param>
        /// <returns></returns>
        public async Task<ExportResult> ExportBatchCoreAsync(
            IEnumerable<(string TenantId, string AgentId, List<Activity> Activities)> groups,
            Resource resource,
            Agent365ExporterOptions options,
            Func<string, string, Task<string?>> tokenResolver,
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        {
            foreach (var g in groups)
            {
                var (tenantId, agentId, activities) = g;
                var json = _formatter.FormatMany(activities, resource);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpointOverride = Environment.GetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE");
                var endpoint = !string.IsNullOrEmpty(endpointOverride)
                    ? endpointOverride
                    : options.DomainResolver.Invoke(tenantId);

                var endpointPath = BuildEndpointPath(tenantId, agentId, options.UseS2SEndpoint);
                var requestUri = BuildRequestUri(endpoint, endpointPath);

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = content
                };

                string? token = null;
                try
                {
                    token = await tokenResolver(agentId, tenantId).ConfigureAwait(false);
                    this._logger?.LogInformation($"Agent365ExporterCore: Obtained token for agent {agentId} tenant {tenantId}.");
                }
                catch (Exception ex)
                {
                    this._logger?.LogError(ex, $"Agent365ExporterCore: TokenResolver threw for agent {agentId} tenant {tenantId}.");
                }

                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage? resp = null;
                try
                {
                    this._logger?.LogInformation($"Agent365ExporterCore: Sending {activities.Count} spans to {requestUri} for agent {agentId} tenant {tenantId}.");
                    resp = await sendAsync(request).ConfigureAwait(false);
                    var correlationId = resp.Headers.Contains(CorrelationIdHeaderKey) ? resp.Headers.GetValues(CorrelationIdHeaderKey).FirstOrDefault() : null;
                    this._logger?.LogInformation($"Agent365ExporterCore: HTTP {(int)resp.StatusCode} exporting spans for agent {agentId} tenant {tenantId}. '{CorrelationIdHeaderKey}': '{correlationId}'.");
                    if (!resp.IsSuccessStatusCode)
                        return ExportResult.Failure;
                }
                catch (Exception ex)
                {
                    this._logger?.LogError(ex, $"Agent365ExporterCore: Exception exporting spans for agent {agentId} tenant {tenantId}.");
                    return ExportResult.Failure;
                }
                finally
                {
                    resp?.Dispose();
                }
            }
            return ExportResult.Success;
        }

        private static void TryAddActivityToMap(Activity activity, Dictionary<(string tenant, string agent), List<Activity>> map)
        {
            if (activity is null) return;

            var tenant = activity.GetAttributeOrBaggage(OpenTelemetryConstants.TenantIdKey);
            var agent = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentIdKey) ?? activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentPlatformIdKey);

            if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(agent))
                return;

            var key = (tenant!, agent!);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<Activity>();
                map[key] = list;
            }
            list.Add(activity);
        }
    }
}