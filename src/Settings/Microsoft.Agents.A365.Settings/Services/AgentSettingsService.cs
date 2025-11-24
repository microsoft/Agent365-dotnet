// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Settings.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Settings.Services
{
    /// <summary>
    /// Provides services for managing agent settings templates and agent instance settings.
    /// </summary>
    public class AgentSettingsService : IAgentSettingsService
    {
        private readonly ILogger<AgentSettingsService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="AgentSettingsService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for logging.</param>
        /// <param name="configuration">Configuration collection.</param>
        /// <param name="httpClient">HTTP client for making API requests.</param>
        public AgentSettingsService(
            ILogger<AgentSettingsService> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <inheritdoc/>
        public async Task<AgentSettingsTemplate?> GetSettingsTemplateByAgentTypeAsync(
            string agentType,
            string authToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentType))
            {
                throw new ArgumentException("Agent type cannot be null or empty.", nameof(agentType));
            }

            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));
            }

            var endpoint = $"{GetPlatformBaseUrl()}/agents/types/{Uri.EscapeDataString(agentType)}/settings/template";
            _logger.LogInformation("Getting settings template for agent type: {AgentType}", agentType);

            try
            {
                using var request = CreateRequest(HttpMethod.Get, endpoint, authToken);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Settings template not found for agent type: {AgentType}", agentType);
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettingsTemplate>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to get settings template for agent type: {AgentType}", agentType);
                throw new InvalidOperationException($"Failed to get settings template for agent type '{agentType}'.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<AgentSettingsTemplate> SetSettingsTemplateByAgentTypeAsync(
            string agentType,
            AgentSettingsTemplate template,
            string authToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentType))
            {
                throw new ArgumentException("Agent type cannot be null or empty.", nameof(agentType));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));
            }

            var endpoint = $"{GetPlatformBaseUrl()}/agents/types/{Uri.EscapeDataString(agentType)}/settings/template";
            _logger.LogInformation("Setting settings template for agent type: {AgentType}", agentType);

            try
            {
                var json = JsonSerializer.Serialize(template, _jsonOptions);
                using var request = CreateRequest(HttpMethod.Put, endpoint, authToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettingsTemplate>(content, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to set settings template for agent type: {AgentType}", agentType);
                throw new InvalidOperationException($"Failed to set settings template for agent type '{agentType}'.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<AgentSettings?> GetSettingsByAgentInstanceAsync(
            string agentInstanceId,
            string authToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentInstanceId))
            {
                throw new ArgumentException("Agent instance ID cannot be null or empty.", nameof(agentInstanceId));
            }

            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));
            }

            var endpoint = $"{GetPlatformBaseUrl()}/agents/{Uri.EscapeDataString(agentInstanceId)}/settings";
            _logger.LogInformation("Getting settings for agent instance: {AgentInstanceId}", agentInstanceId);

            try
            {
                using var request = CreateRequest(HttpMethod.Get, endpoint, authToken);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Settings not found for agent instance: {AgentInstanceId}", agentInstanceId);
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettings>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to get settings for agent instance: {AgentInstanceId}", agentInstanceId);
                throw new InvalidOperationException($"Failed to get settings for agent instance '{agentInstanceId}'.", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<AgentSettings> SetSettingsByAgentInstanceAsync(
            string agentInstanceId,
            AgentSettings settings,
            string authToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(agentInstanceId))
            {
                throw new ArgumentException("Agent instance ID cannot be null or empty.", nameof(agentInstanceId));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));
            }

            var endpoint = $"{GetPlatformBaseUrl()}/agents/{Uri.EscapeDataString(agentInstanceId)}/settings";
            _logger.LogInformation("Setting settings for agent instance: {AgentInstanceId}", agentInstanceId);

            try
            {
                var json = JsonSerializer.Serialize(settings, _jsonOptions);
                using var request = CreateRequest(HttpMethod.Put, endpoint, authToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AgentSettings>(content, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to set settings for agent instance: {AgentInstanceId}", agentInstanceId);
                throw new InvalidOperationException($"Failed to set settings for agent instance '{agentInstanceId}'.", ex);
            }
        }

        private string GetPlatformBaseUrl()
        {
            // Check for configuration override first
            var configuredUrl = _configuration[Constants.PlatformEndpointConfigKey];
            if (!string.IsNullOrEmpty(configuredUrl))
            {
                // Validate the configured URL is a valid URI
                if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    _logger.LogWarning("Invalid platform URL configured: {Url}. Using default.", configuredUrl);
                    return Constants.DefaultPlatformBaseUrl;
                }

                return configuredUrl.TrimEnd('/');
            }

            return Constants.DefaultPlatformBaseUrl;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, string authToken)
        {
            var request = new HttpRequestMessage(method, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            return request;
        }
    }
}
