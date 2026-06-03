// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
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

        // The ingest service performs a case-insensitive check for "chat", so we send the
        // gen_ai.operation.name through unchanged. Both the lowercase canonical value and the
        // InferenceOperationType.Chat enum name are accepted in this set so that activities
        // tagged with either form are not filtered out by PartitionByIdentity.
        private static readonly HashSet<string> GenAiOperationNames = new HashSet<string>(StringComparer.Ordinal)
        {
            InvokeAgentScope.OperationName,
            ExecuteToolScope.OperationName,
            OutputScope.OperationName,
            OpenTelemetryConstants.ApplyGuardrailOperationName,
            "chat",
            nameof(InferenceOperationType.Chat),
        };

        private enum AddResult { Added, NonGenAI, MissingIdentity, Null }

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
        /// Only genAI activities (those with a known gen_ai.operation.name) are included.
        /// </summary>
        /// <param name="batch">The collection of activities to partition.</param>
        /// <returns>
        /// A list of tuples containing TenantId, AgentId, and the corresponding activities.
        /// </returns>
        public List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(IEnumerable<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();
            int nonGenAICount = 0;
            int missingIdentityCount = 0;

            foreach (var activity in batch)
            {
                var result = Agent365ExporterCore.TryAddActivityToMap(activity, map);
                if (result == AddResult.NonGenAI) nonGenAICount++;
                else if (result == AddResult.MissingIdentity) missingIdentityCount++;
            }

            LogPartitionResults(map.Count, nonGenAICount, missingIdentityCount);
            return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
        }

        /// <summary>
        /// Partitions a batch of activities by tenant and agent identity.
        /// Only genAI activities (those with a known gen_ai.operation.name) are included.
        /// </summary>
        /// <param name="batch">The collection of activities to partition.</param>
        /// <returns>
        /// A list of tuples containing TenantId, AgentId, and the corresponding activities.
        /// </returns>
        public List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(in Batch<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();
            int nonGenAICount = 0;
            int missingIdentityCount = 0;

            foreach (var activity in batch)
            {
                var result = Agent365ExporterCore.TryAddActivityToMap(activity, map);
                if (result == AddResult.NonGenAI) nonGenAICount++;
                else if (result == AddResult.MissingIdentity) missingIdentityCount++;
            }

            LogPartitionResults(map.Count, nonGenAICount, missingIdentityCount);
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
                ? $"/observabilityService/tenants/{encodedTenantId}/otlp/agents/{encodedAgentId}/traces"
                : $"/observability/tenants/{encodedTenantId}/otlp/agents/{encodedAgentId}/traces";
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

                // Split the per-identity batch into byte-size chunks under MaxPayloadBytes.
                // Per-span truncation already caps individual spans at 250 KB; this provides
                // batch-level enforcement of the 1 MB server limit.
                var chunks = PayloadChunking.ChunkBySize(
                    activities,
                    PayloadChunking.EstimateActivityBytes,
                    options.MaxPayloadBytes);

                if (chunks.Count > 1)
                {
                    this._logger?.LogInformation(
                        "Agent365ExporterCore: Split {SpanCount} spans into {ChunkCount} chunks for tenantId {TenantId}, agentId {AgentId}.",
                        activities.Count, chunks.Count, tenantId, agentId);
                }

                var endpointOverride = Environment.GetEnvironmentVariable("A365_OBSERVABILITY_DOMAIN_OVERRIDE");
                var endpoint = !string.IsNullOrEmpty(endpointOverride)
                    ? endpointOverride
                    : options.DomainResolver.Invoke(tenantId);

                var endpointPath = BuildEndpointPath(tenantId, agentId, options.UseS2SEndpoint);
                var requestUri = BuildRequestUri(endpoint, endpointPath);

                string? token = null;
                try
                {
                    // Prefer ContextualTokenResolver when set; extract agentic user ID from the
                    // first activity in the group (1:1 relationship between agent and agentic user).
                    if (options.ContextualTokenResolver != null)
                    {
                        var agenticUserId = activities.Count > 0
                            ? activities[0].GetAttributeOrBaggage(OpenTelemetryConstants.AgentAUIDKey)
                            : null;
                        var identity = new AgentIdentity(agentId, agenticUserId);

                        var context = new TokenResolverContext(identity, tenantId);
                        token = await options.ContextualTokenResolver(context).ConfigureAwait(false);
                    }
                    else
                    {
                        token = await tokenResolver(agentId, tenantId).ConfigureAwait(false);
                    }

                    this._logger?.LogDebug("Agent365ExporterCore: Obtained token for agent {AgentId} tenant {TenantId}.", agentId, tenantId);
                }
                catch (Exception ex)
                {
                    this._logger?.LogError(ex, "Agent365ExporterCore: TokenResolver threw for agent {AgentId} tenant {TenantId}.", agentId, tenantId);
                }

                if (string.IsNullOrEmpty(token))
                {
                    this._logger?.LogWarning("Agent365ExporterCore: No token obtained. Skipping export for this identity.");
                    return ExportResult.Failure;
                }

                // Send each chunk; all-or-nothing: fail on first chunk failure so the batch processor retries.
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    var json = _formatter.FormatMany(chunk, resource);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                    {
                        Content = content
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var bodyBytes = Encoding.UTF8.GetByteCount(json);
                    this._logger?.LogDebug(
                        "Agent365ExporterCore: Sending chunk {ChunkIndex} of {ChunkCount} ({SpanCount} spans, {BodyBytes} bytes) to {RequestUri}.",
                        i + 1, chunks.Count, chunk.Count, bodyBytes, requestUri);

                    try
                    {
                        using var resp = await sendAsync(request).ConfigureAwait(false);
                        var correlationId = resp.Headers.Contains(CorrelationIdHeaderKey) ? resp.Headers.GetValues(CorrelationIdHeaderKey).FirstOrDefault() : null;
                        this._logger?.LogDebug("Agent365ExporterCore: HTTP {StatusCode} exporting spans. '{HeaderKey}': '{CorrelationId}'.", (int)resp.StatusCode, CorrelationIdHeaderKey, correlationId);
                        if (!resp.IsSuccessStatusCode)
                        {
                            this._logger?.LogWarning(
                                "Agent365ExporterCore: Chunk {ChunkIndex} of {ChunkCount} failed; aborting batch.",
                                i + 1, chunks.Count);
                            return ExportResult.Failure;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        this._logger?.LogError(ex, "Agent365ExporterCore: Exception exporting spans.");
                        return ExportResult.Failure;
                    }
                    catch (TaskCanceledException ex)
                    {
                        this._logger?.LogError(ex, "Agent365ExporterCore: Exception exporting spans.");
                        return ExportResult.Failure;
                    }
                }
            }
            return ExportResult.Success;
        }

        private static AddResult TryAddActivityToMap(Activity activity, Dictionary<(string tenant, string agent), List<Activity>> map)
        {
            if (activity is null) return AddResult.Null;

            var operationName = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiOperationNameKey);
            if (string.IsNullOrEmpty(operationName) || !GenAiOperationNames.Contains(operationName!))
                return AddResult.NonGenAI;

            var tenant = activity.GetAttributeOrBaggage(OpenTelemetryConstants.TenantIdKey);
            var agent = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentIdKey) ?? activity.GetAttributeOrBaggage(OpenTelemetryConstants.AgentPlatformIdKey);

            if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(agent))
                return AddResult.MissingIdentity;

            var key = (tenant!, agent!);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<Activity>();
                map[key] = list;
            }
            list.Add(activity);
            return AddResult.Added;
        }

        private void LogPartitionResults(int groupCount, int nonGenAICount, int missingIdentityCount)
        {
            if (nonGenAICount > 0)
                _logger?.LogDebug("[Agent365Exporter] {NonGenAICount} non-genAI spans filtered out", nonGenAICount);
            if (missingIdentityCount > 0)
                _logger?.LogDebug("[Agent365Exporter] {MissingIdentityCount} spans skipped due to missing tenant or agent ID", missingIdentityCount);

            var skippedCount = nonGenAICount + missingIdentityCount;
            if (skippedCount > 0)
                _logger?.LogDebug("[Agent365Exporter] Partitioned into {GroupCount} identity groups ({SkippedCount} spans skipped)", groupCount, skippedCount);
        }
    }
}