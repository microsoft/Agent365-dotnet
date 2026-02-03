// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Transports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Microsoft.Agents.A365.Tooling.Services
{
    /// <summary>
    /// Implementation of <see cref="IMcpServerDiscoveryService"/> that discovers MCP servers
    /// from cloud (ATG) and local (Windows desktop) sources.
    /// </summary>
    public class McpServerDiscoveryService : IMcpServerDiscoveryService
    {
        private readonly ILogger<McpServerDiscoveryService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMcpToolServerConfigurationService _mcpConfigService;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerDiscoveryService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="configuration">Configuration.</param>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="mcpConfigService">MCP configuration service for ATG access.</param>
        public McpServerDiscoveryService(
            ILogger<McpServerDiscoveryService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IMcpToolServerConfigurationService mcpConfigService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _mcpConfigService = mcpConfigService;
        }

        /// <inheritdoc/>
        public async Task<List<MCPServerConfig>> DiscoverAllServersAsync(
            string agentInstanceId,
            string authToken,
            string? clientName,
            ToolOptions toolOptions,
            CancellationToken cancellationToken = default)
        {
            var allServers = new List<MCPServerConfig>();

            // 1. Discover cloud servers from ATG
            try
            {
                var cloudServers = await DiscoverCloudServersAsync(agentInstanceId, authToken, toolOptions, cancellationToken);
                allServers.AddRange(cloudServers);
                _logger.LogInformation("[Discovery] Found {Count} cloud MCP servers from ATG", cloudServers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Discovery] Failed to discover cloud MCP servers from ATG");
            }

            // 2. Discover local servers from Windows desktop (if client name provided)
            if (!string.IsNullOrEmpty(clientName))
            {
                var proxyBaseUrl = _configuration["LocalMcp:BaseUrl"];
                if (!string.IsNullOrEmpty(proxyBaseUrl))
                {
                    try
                    {
                        var localServers = await DiscoverLocalServersAsync(clientName, proxyBaseUrl, cancellationToken);
                        allServers.AddRange(localServers);
                        _logger.LogInformation("[Discovery] Found {Count} local MCP servers from desktop client '{ClientName}'",
                            localServers.Count, clientName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Discovery] Failed to discover local MCP servers from desktop client '{ClientName}'", clientName);
                    }
                }
                else
                {
                    _logger.LogDebug("[Discovery] LocalMcp:BaseUrl not configured, skipping local server discovery");
                }
            }

            _logger.LogInformation("[Discovery] Total discovered MCP servers: {Count}", allServers.Count);
            return allServers;
        }

        /// <inheritdoc/>
        public Task<List<MCPServerConfig>> DiscoverCloudServersAsync(
            string agentInstanceId,
            string authToken,
            ToolOptions toolOptions,
            CancellationToken cancellationToken = default)
        {
            // Delegate to existing McpToolServerConfigurationService which handles ATG
            return _mcpConfigService.ListToolServersAsync(agentInstanceId, authToken, toolOptions);
        }

        /// <inheritdoc/>
        public async Task<List<MCPServerConfig>> DiscoverLocalServersAsync(
            string clientName,
            string proxyBaseUrl,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[Discovery] Requesting local MCP server list from desktop client '{ClientName}'", clientName);

            var httpClient = _httpClientFactory.CreateClient("LocalMcpDiscovery");

            try
            {
                // Step 1: Send a "list_servers" request via the WNS proxy
                var request = new ListLocalMcpServersRequest
                {
                    Type = "list_servers",
                    RequestId = Guid.NewGuid().ToString()
                };

                var requestJson = JsonSerializer.Serialize(request);

                // Send notification to wake up desktop and request server list
                var notifyResponse = await httpClient.PostAsync(
                    $"{proxyBaseUrl}/api/notify/{clientName}",
                    new StringContent(requestJson, Encoding.UTF8, "application/json"),
                    cancellationToken);

                notifyResponse.EnsureSuccessStatusCode();

                var notifyResult = await notifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var sessionId = notifyResult.GetProperty("sessionId").GetString()
                    ?? throw new InvalidOperationException("Failed to get sessionId from notify response");

                _logger.LogDebug("[Discovery] Session {SessionId} created, waiting for desktop connection...", sessionId);

                // Step 2: Wait for desktop to connect
                var timeout = TimeSpan.FromSeconds(_configuration.GetValue("LocalMcp:ConnectionTimeoutSeconds", 30));
                var connected = await WaitForConnectionAsync(httpClient, proxyBaseUrl, sessionId, timeout, cancellationToken);

                if (!connected)
                {
                    throw new TimeoutException($"Desktop client '{clientName}' did not connect within {timeout.TotalSeconds}s");
                }

                // Wait for WebSocket to be ready
                await Task.Delay(1000, cancellationToken);

                // Step 3: Send the list_servers request via MCP proxy endpoint
                var listRequest = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "list_servers",
                    @params = new { }
                };

                var listRequestJson = JsonSerializer.Serialize(listRequest);

                var listResponse = await httpClient.PostAsync(
                    $"{proxyBaseUrl}/api/mcp/{sessionId}",
                    new StringContent(listRequestJson, Encoding.UTF8, "application/json"),
                    cancellationToken);

                listResponse.EnsureSuccessStatusCode();

                var listResult = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

                // Parse the response
                if (listResult.TryGetProperty("result", out var resultElement) &&
                    resultElement.TryGetProperty("servers", out var serversElement))
                {
                    var localServers = JsonSerializer.Deserialize<List<LocalMcpServerInfo>>(serversElement.GetRawText())
                        ?? new List<LocalMcpServerInfo>();

                    return ConvertLocalServersToConfig(localServers, clientName, proxyBaseUrl);
                }

                // If no servers found or different response format
                _logger.LogWarning("[Discovery] No servers found in list_servers response or unexpected format");
                return new List<MCPServerConfig>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Discovery] Failed to discover local MCP servers from '{ClientName}'", clientName);
                throw;
            }
        }

        /// <inheritdoc/>
        public List<MCPServerConfig> ConvertLocalServersToConfig(
            List<LocalMcpServerInfo> localServers,
            string clientName,
            string proxyBaseUrl)
        {
            var configs = new List<MCPServerConfig>();

            foreach (var server in localServers)
            {
                // ServerId is derived from packages[0].identifier
                var serverId = server.ServerId;
                if (string.IsNullOrEmpty(serverId))
                {
                    _logger.LogWarning("[Discovery] Skipping local server '{Name}' with no package identifier", server.Name);
                    continue;
                }

                // Use direct properties from the odr mcp list response
                var serverName = server.Name ?? serverId;
                var description = server.Description ?? string.Empty;

                // Extract static tools from the discovery response (avoids needing tools/list call later)
                var staticTools = server.GetStaticToolsList();
                if (staticTools != null)
                {
                    var toolNames = new List<string>();
                    foreach (var tool in staticTools.Value.EnumerateArray())
                    {
                        if (tool.TryGetProperty("name", out var nameElement))
                        {
                            toolNames.Add(nameElement.GetString() ?? "unknown");
                        }
                    }
                    _logger.LogInformation("[Discovery] Server '{ServerName}' has {Count} static tools: [{ToolNames}]",
                        serverName, staticTools.Value.GetArrayLength(), string.Join(", ", toolNames));
                }
                else
                {
                    _logger.LogWarning("[Discovery] Server '{ServerName}' has NO static tools extracted from _meta", serverName);
                }

                var config = new MCPServerConfig
                {
                    mcpServerName = serverName,
                    id = serverId,
                    url = string.Empty, // Not needed for WNS transport
                    scope = string.Empty,
                    audience = string.Empty,
                    publisher = "Local",
                    transportType = McpTransportType.Wns,
                    staticToolsList = staticTools,
                    wnsConfig = new WnsTransportConfig
                    {
                        clientName = clientName,
                        proxyBaseUrl = proxyBaseUrl,
                        localServerId = serverId,
                        connectionTimeoutSeconds = _configuration.GetValue("LocalMcp:ConnectionTimeoutSeconds", 30)
                    }
                };

                configs.Add(config);
                _logger.LogDebug("[Discovery] Converted local server: {ServerName} ({ServerId}), hasStaticTools: {HasStatic}",
                    serverName, serverId, config.HasStaticTools);
            }

            return configs;
        }

        private async Task<bool> WaitForConnectionAsync(
            HttpClient httpClient,
            string proxyBaseUrl,
            string sessionId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await httpClient.GetAsync(
                        $"{proxyBaseUrl}/api/status/{sessionId}",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var status = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                        if (status.GetProperty("connected").GetBoolean())
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[Discovery] Connection check failed");
                }

                await Task.Delay(1000, cancellationToken);
            }

            return false;
        }
    }
}
