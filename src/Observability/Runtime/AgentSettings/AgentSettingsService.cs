// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Observability.Runtime.Common;

namespace Microsoft.Agents.A365.Observability.Runtime.AgentSettings
{
    /// <summary>
    /// Service for managing agent configuration templates and instance-specific settings.
    /// </summary>
    public class AgentSettingsService
    {
        private readonly PowerPlatformApiDiscovery _apiDiscovery;
        private readonly string _tenantId;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentSettingsService"/> class.
        /// </summary>
        /// <param name="apiDiscovery">The Power Platform API discovery service.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="httpClient">Optional HttpClient instance. If not provided, a new instance will be created.</param>
        public AgentSettingsService(
            PowerPlatformApiDiscovery apiDiscovery,
            string tenantId,
            HttpClient? httpClient = null)
        {
            _apiDiscovery = apiDiscovery ?? throw new ArgumentNullException(nameof(apiDiscovery));
            _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// Gets the agent setting template for a specific agent type.
        /// </summary>
        /// <param name="agentType">The agent type identifier.</param>
        /// <param name="token">The authentication token.</param>
        /// <returns>The agent setting template.</returns>
        public async Task<AgentSettingTemplate?> GetAgentSettingTemplateAsync(string agentType, string token)
        {
            if (string.IsNullOrEmpty(agentType))
            {
                throw new ArgumentNullException(nameof(agentType));
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var endpoint = _apiDiscovery.GetTenantEndpoint(_tenantId);
            var url = $"https://{endpoint}/agents/v1.0/settings/templates/{Uri.EscapeDataString(agentType)}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettingTemplate>(content);
            }
        }

        /// <summary>
        /// Sets the agent setting template for a specific agent type.
        /// </summary>
        /// <param name="template">The agent setting template to set.</param>
        /// <param name="token">The authentication token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetAgentSettingTemplateAsync(AgentSettingTemplate template, string token)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (string.IsNullOrEmpty(template.AgentType))
            {
                throw new ArgumentException("AgentType cannot be null or empty.", nameof(template));
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var endpoint = _apiDiscovery.GetTenantEndpoint(_tenantId);
            var url = $"https://{endpoint}/agents/v1.0/settings/templates/{Uri.EscapeDataString(template.AgentType)}";

            var json = JsonSerializer.Serialize(template);
            using (var request = new HttpRequestMessage(HttpMethod.Put, url))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Gets the agent settings for a specific agent instance.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance identifier.</param>
        /// <param name="token">The authentication token.</param>
        /// <returns>The agent settings.</returns>
        public async Task<AgentSettings?> GetAgentSettingsAsync(string agentInstanceId, string token)
        {
            if (string.IsNullOrEmpty(agentInstanceId))
            {
                throw new ArgumentNullException(nameof(agentInstanceId));
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var endpoint = _apiDiscovery.GetTenantEndpoint(_tenantId);
            var url = $"https://{endpoint}/agents/v1.0/settings/instances/{Uri.EscapeDataString(agentInstanceId)}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettings>(content);
            }
        }

        /// <summary>
        /// Sets the agent settings for a specific agent instance.
        /// </summary>
        /// <param name="settings">The agent settings to set.</param>
        /// <param name="token">The authentication token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetAgentSettingsAsync(AgentSettings settings, string token)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrEmpty(settings.AgentInstanceId))
            {
                throw new ArgumentException("AgentInstanceId cannot be null or empty.", nameof(settings));
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            var endpoint = _apiDiscovery.GetTenantEndpoint(_tenantId);
            var url = $"https://{endpoint}/agents/v1.0/settings/instances/{Uri.EscapeDataString(settings.AgentInstanceId)}";

            var json = JsonSerializer.Serialize(settings);
            using (var request = new HttpRequestMessage(HttpMethod.Put, url))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
