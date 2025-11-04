using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Agent365Exporter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">The exporter configuration options.</param>
        /// <param name="resource">Optional OpenTelemetry resource information.</param>
        public Agent365Exporter(ILogger<Agent365Exporter> logger, Agent365ExporterOptions options, Resource? resource = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.TokenResolver == null)
                throw new ArgumentNullException(nameof(options.TokenResolver), "Agent365ExporterOptions.TokenResolver must be provided.");

            _httpClient = new HttpClient();

            _resource = resource ?? ResourceBuilder.CreateEmpty().Build();
        }

        /// <summary>
        /// Exports a batch of OpenTelemetry activities to the Microsoft Agents A365 observability platform.
        /// </summary>
        /// <param name="batch">The batch of activities to export.</param>
        /// <returns>The export result indicating success or failure.</returns>
        public override ExportResult Export(in Batch<Activity> batch)
        {
            var anyFailure = false;
            _logger.LogInformation("Agent365Exporter: Exporting batch of {Count} spans.", batch.Count);

            try
            {
                // Partition by (tenantId, agentId)
                var groups = PartitionByIdentity(batch);
                if (groups.Count == 0)
                {
                    _logger.LogInformation("Agent365Exporter: No spans with tenant/agent identity found; nothing exported.");
                    return ExportResult.Success;
                }

                foreach (var g in groups)
                {
                    var (tenantId, agentId, activities) = g;

                    // Build payload for just this identity
                    var json = ExportFormatter.FormatMany(activities, _resource);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Endpoint/token per identity
                    var ppapiDiscovery = new PowerPlatformApiDiscovery(_options.ClusterCategory);
                    var ppapiEndpoint = ppapiDiscovery.GetTenantIslandClusterEndpoint(tenantId);

                    // Choose endpoint path based on UseS2SEndpoint setting
                    var endpointPath = _options.UseS2SEndpoint
                        ? $"/maven/agent365/service/agents/{agentId}/traces"
                        : $"/maven/agent365/agents/{agentId}/traces";

                    var requestUri = $"https://{ppapiEndpoint}{endpointPath}?api-version=1";

                    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                    {
                        Content = content
                    };

                    string? token = null;
                    try
                    {
                        token = _options.TokenResolver!(agentId, tenantId).GetAwaiter().GetResult();
                        _logger.LogInformation("Agent365Exporter: Obtained token for agent {Agent} tenant {Tenant}.", agentId, tenantId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Agent365Exporter: TokenResolver threw for agent {Agent} tenant {Tenant}.", agentId, tenantId);
                    }

                    if (!string.IsNullOrEmpty(token))
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    HttpResponseMessage? resp = null;
                    try
                    {
                        _logger.LogInformation("Agent365Exporter: Sending {Count} spans to {Uri} for agent {Agent} tenant {Tenant}.", activities.Count, requestUri, agentId, tenantId);

                        resp = _httpClient.SendAsync(request).GetAwaiter().GetResult();

                        if (resp.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Agent365Exporter: HTTP {Status} exporting spans for agent {Agent} tenant {Tenant}.", (int)resp.StatusCode, agentId, tenantId);
                        }
                        else
                        {
                            anyFailure = true;
                            _logger.LogWarning("Agent365Exporter: HTTP {Status} exporting spans for agent {Agent} tenant {Tenant}.", (int)resp.StatusCode, agentId, tenantId);
                        }
                    }
                    catch (Exception ex)
                    {
                        anyFailure = true;
                        _logger.LogError(ex, "Agent365Exporter: Exception exporting spans for agent {Agent} tenant {Tenant}.", agentId, tenantId);
                    }
                    finally
                    {
                        resp?.Dispose();
                    }
                }
            }
            catch (Exception exOuter)
            {
                _logger.LogError(exOuter, "Agent365Exporter: Unhandled export exception.");
                return ExportResult.Failure;
            }

            return anyFailure ? ExportResult.Failure : ExportResult.Success;
        }

        // Extract (tenant, agent) per activity. Prefer tags; fallback to per-activity baggage.
        private List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(in Batch<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();

            foreach (var activity in batch)
            {
                if (activity is null) continue;

                var tenant = activity.GetAttributeOrBaggage(OpenTelemetryConstants.TenantIdKey);
                var agent = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentIdKey);

                if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(agent))
                    continue; // skip spans without identity (could log once with a counter)

                // At this point, tenant and agent are guaranteed to be non-null and non-empty
                var key = (tenant!, agent!);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<Activity>();
                    map[key] = list;
                }
                list.Add(activity);
            }

            return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
        }
    }
}

