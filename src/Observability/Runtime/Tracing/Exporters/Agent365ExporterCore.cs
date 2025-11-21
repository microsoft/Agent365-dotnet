// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
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
    public static class Agent365ExporterCore
    {
        private const string CorrelationIdHeaderKey = "x-ms-correlation-id";
        private const int MaxActivitySizeBytes = 250 * 1024;
        private static readonly string[] LargePayloadAttributeKeys = new[]
        {
            OpenTelemetryConstants.GenAiToolArgumentsKey,
            OpenTelemetryConstants.GenAiEventContent,
            OpenTelemetryConstants.GenAiInputMessagesKey,
            OpenTelemetryConstants.GenAiInvocationInputKey,
            OpenTelemetryConstants.GenAiOutputMessagesKey,
            OpenTelemetryConstants.GenAiInvocationOutputKey
        };

        /// <summary>
        /// Truncates the largest-to-smallest of the specified activity attributes until the activity's serialized size is under 250 KB.
        /// Logs the size of each key/value and each truncation.
        /// </summary>
        /// <param name="activity">The activity to check and potentially truncate.</param>
        /// <param name="resource">The resource for serialization context.</param>
        /// <param name="logInformation">Logger for informational messages.</param>
        /// <returns>True if any truncation occurred, otherwise false.</returns>
        public static bool TruncateActivityToMaxSize(
            Activity activity,
            Resource resource,
            Action<string>? logInformation = null)
        {
            if (activity == null) return false;

            // Check initial size
            string json = ExportFormatter.FormatSingle(activity, resource);
            if (Encoding.UTF8.GetByteCount(json) <= Agent365ExporterCore.MaxActivitySizeBytes)
                return false;

            // Get all key/value sizes and log them
            var keySizes = new List<(string Key, int Size, string? Value)>();
            foreach (var key in Agent365ExporterCore.LargePayloadAttributeKeys)
            {
                var value = activity.GetTagItem(key) as string;
                int size = !string.IsNullOrEmpty(value) ? Encoding.UTF8.GetByteCount(value) : 0;
                keySizes.Add((key, size, value));
                logInformation?.Invoke($"Activity '{activity.DisplayName}': Key '{key}' size = {size / 1024} KB.");
            }

            // Sort keys by size descending
            var sorted = keySizes
                .Where(k => !string.IsNullOrEmpty(k.Value) && k.Size > 0)
                .OrderByDescending(k => k.Size)
                .ToList();

            foreach (var (key, size, _) in sorted)
            {
                activity.SetTag(key, "TRUNCATED");
                logInformation?.Invoke($"Truncated '{key}' in activity '{activity.DisplayName}' to reduce size. Previous size: {size / 1024} KB.");

                // Re-check size after each truncation
                json = ExportFormatter.FormatSingle(activity, resource);
                if (Encoding.UTF8.GetByteCount(json) <= Agent365ExporterCore.MaxActivitySizeBytes)
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Partitions a batch of activities by tenant and agent identity.
        /// </summary>
        /// <param name="batch">The collection of activities to partition.</param>
        /// <returns>
        /// A list of tuples containing TenantId, AgentId, and the corresponding activities.
        /// </returns>
        public static List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(IEnumerable<Activity> batch)
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
        public static List<(string TenantId, string AgentId, List<Activity> Activities)> PartitionByIdentity(in Batch<Activity> batch)
        {
            var map = new Dictionary<(string tenant, string agent), List<Activity>>();

            foreach (var activity in batch)
            {
                Agent365ExporterCore.TryAddActivityToMap(activity, map);
            }

            return map.Select(kvp => (kvp.Key.tenant, kvp.Key.agent, kvp.Value)).ToList();
        }

        /// <summary>
        /// Builds the endpoint path for the trace export request based on agent ID and S2S setting.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="useS2SEndpoint">Whether to use the S2S endpoint.</param>
        /// <returns>The endpoint path string.</returns>
        public static string BuildEndpointPath(string agentId, bool useS2SEndpoint)
        {
            return useS2SEndpoint
                ? $"/maven/agent365/service/agents/{agentId}/traces"
                : $"/maven/agent365/agents/{agentId}/traces";
        }

        /// <summary>
        /// Builds the full request URI for the trace export request.
        /// </summary>
        /// <param name="endpoint">The base endpoint.</param>
        /// <param name="endpointPath">The endpoint path.</param>
        /// <returns>The full request URI string.</returns>
        public static string BuildRequestUri(string endpoint, string endpointPath)
        {
            return $"https://{endpoint}{endpointPath}?api-version=1";
        }

        /// <summary>
        /// Exports a batch of activities grouped by tenant and agent identity.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="resource"></param>
        /// <param name="options"></param>
        /// <param name="tokenResolver"></param>
        /// <param name="sendAsync"></param>
        /// <param name="logInformation"></param>
        /// <param name="logError"></param>
        /// <returns></returns>
        public static async Task<ExportResult> ExportBatchCoreAsync(
            IEnumerable<(string TenantId, string AgentId, List<Activity> Activities)> groups,
            Resource resource,
            Agent365ExporterOptions options,
            Func<string, string, Task<string?>> tokenResolver,
            Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync,
            Action<string>? logInformation = null,
            Action<Exception, string>? logError = null)
        {
            foreach (var g in groups)
            {
                var (tenantId, agentId, activities) = g;

                // Truncate activities if needed before serialization
                foreach (var activity in activities)
                {
                    Agent365ExporterCore.TruncateActivityToMaxSize(activity, resource, logInformation);
                }

                var json = ExportFormatter.FormatMany(activities, resource);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var ppapiDiscovery = new PowerPlatformApiDiscovery(options.ClusterCategory);
                var ppapiEndpoint = ppapiDiscovery.GetTenantIslandClusterEndpoint(tenantId);

                var endpointPath = Agent365ExporterCore.BuildEndpointPath(agentId, options.UseS2SEndpoint);
                var requestUri = Agent365ExporterCore.BuildRequestUri(ppapiEndpoint, endpointPath);

                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = content
                };

                string? token = null;
                try
                {
                    token = await tokenResolver(agentId, tenantId).ConfigureAwait(false);
                    logInformation?.Invoke($"Obtained token for agent {agentId} tenant {tenantId}.");
                }
                catch (Exception ex)
                {
                    logError?.Invoke(ex, $"TokenResolver threw for agent {agentId} tenant {tenantId}.");
                }

                if (!string.IsNullOrEmpty(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage? resp = null;
                try
                {
                    logInformation?.Invoke($"Sending {activities.Count} spans to {requestUri} for agent {agentId} tenant {tenantId}.");
                    resp = await sendAsync(request).ConfigureAwait(false);
                    var correlationId = resp.Headers.Contains(Agent365ExporterCore.CorrelationIdHeaderKey) ? resp.Headers.GetValues(Agent365ExporterCore.CorrelationIdHeaderKey).FirstOrDefault() : null;
                    logInformation?.Invoke($"HTTP {(int)resp.StatusCode} exporting spans for agent {agentId} tenant {tenantId}. '{Agent365ExporterCore.CorrelationIdHeaderKey}': '{correlationId}'.");
                    if (!resp.IsSuccessStatusCode)
                        return ExportResult.Failure;
                }
                catch (Exception ex)
                {
                    logError?.Invoke(ex, $"Exception exporting spans for agent {agentId} tenant {tenantId}.");
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
            var agent = activity.GetAttributeOrBaggage(OpenTelemetryConstants.GenAiAgentIdKey);

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