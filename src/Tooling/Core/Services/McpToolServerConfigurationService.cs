// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Tooling.Exceptions;
    using Microsoft.Agents.A365.Tooling.Handlers;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Transports;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Client;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;

    /// <summary>
    /// Provides services for managing MCP server configurations.
    /// </summary>
    public class McpToolServerConfigurationService : IMcpToolServerConfigurationService
    {
        private readonly ILogger<IMcpToolServerConfigurationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILocalMcpScopeValidator? _localMcpScopeValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolServerConfigurationService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for logging.</param>
        /// <param name="configuration">Configuration collection.</param>
        /// <param name="serviceProvider">Service provider</param>
        /// <param name="httpClientFactory">HTTP client factory for creating HTTP clients.</param>
        public McpToolServerConfigurationService(ILogger<IMcpToolServerConfigurationService> logger, IConfiguration configuration, IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            this._configuration = configuration;
            this._logger = logger;
            this._loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            this._httpClientFactory = httpClientFactory;
            this._localMcpScopeValidator = serviceProvider.GetService<ILocalMcpScopeValidator>();
        }

        /// <inheritdoc/>
        public async Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken)
        {
            return await ListToolServersAsync(agentInstanceId, authToken, new ToolOptions());
        }

        /// <inheritdoc/>
        public Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken, ToolOptions toolOptions)
        {
            // Always read from the local manifest file for now
            // This allows local MCP servers (with WNS transport) to be configured alongside cloud servers
            return Task.FromResult(GetMCPServersFromManifest());
        }

        /// <inheritdoc/>
        public async Task<IList<McpClientTool>> GetMcpClientToolsAsync(
            ITurnContext turnContext,
            MCPServerConfig mCPServerConfig,
            string authToken,
            ToolOptions toolOptions)
        {
            try
            {
                // Validate the server name
                if (string.IsNullOrWhiteSpace(mCPServerConfig.mcpServerName))
                {
                    throw new ArgumentException("MCP Server name cannot be null or empty", nameof(mCPServerConfig.mcpServerName));
                }

                this._logger.LogInformation("[GetTools] Creating MCP client for: {ServerName} (transport: {Transport}, hasStaticTools: {HasStatic})",
                    mCPServerConfig.mcpServerName, mCPServerConfig.transportType, mCPServerConfig.HasStaticTools);

                IMcpClient mcpClient;

                // Create appropriate client based on transport type
                switch (mCPServerConfig.transportType)
                {
                    case McpTransportType.Wns:
                        this._logger.LogInformation("[GetTools] Using WNS transport for {ServerName}", mCPServerConfig.mcpServerName);

                        // Validate local MCP server scope before invocation
                        // This is equivalent to how remote MCP servers validate scope via token
                        if (this._localMcpScopeValidator != null)
                        {
                            var scopeResult = await this._localMcpScopeValidator.ValidateScopeAsync(mCPServerConfig.mcpServerName);
                            if (!scopeResult.IsValid)
                            {
                                throw new UnauthorizedAccessException(
                                    $"[LocalMcpScope] Access denied to local MCP server '{mCPServerConfig.mcpServerName}': {scopeResult.ErrorMessage}");
                            }
                            this._logger.LogInformation("[GetTools] Scope validation passed for local server {ServerName}", mCPServerConfig.mcpServerName);
                        }
                        else
                        {
                            this._logger.LogDebug("[GetTools] Local MCP scope validator not registered, skipping scope validation");
                        }

                        mcpClient = await CreateWnsMcpClientAsync(mCPServerConfig);
                        break;

                    case McpTransportType.WebSocket:
                        throw new NotSupportedException("WebSocket transport is not yet implemented");

                    case McpTransportType.Sse:
                    default:
                        this._logger.LogInformation("[GetTools] Using SSE transport for {ServerName}", mCPServerConfig.mcpServerName);
                        mcpClient = await CreateMcpClientWithAuthHandlers(turnContext, new Uri(mCPServerConfig.url), authToken, toolOptions);
                        break;
                }

                this._logger.LogInformation("[GetTools] MCP client created, calling ListToolsAsync for {ServerName}", mCPServerConfig.mcpServerName);
                var tools = await mcpClient.ListToolsAsync();

                this._logger.LogInformation("[GetTools] Successfully retrieved {ToolCount} tools from {ServerName}", tools.Count, mCPServerConfig.mcpServerName);

                return tools;
            }
            catch (HttpRequestException httpEx)
            {
                throw new InvalidOperationException($"HTTP error connecting to MCP server '{mCPServerConfig.mcpServerName}' at '{mCPServerConfig.url}': {httpEx.Message}", httpEx);
            }
            catch (ArgumentException argEx)
            {
                throw new InvalidOperationException($"Invalid configuration for MCP server '{mCPServerConfig.mcpServerName}': {argEx.Message}", argEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get tools from MCP server '{mCPServerConfig.mcpServerName}' at '{mCPServerConfig.url}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages, CancellationToken cancellationToken = default)
        {
            return await SendChatHistoryAsync(turnContext, chatHistoryMessages, new ToolOptions { UserAgentConfiguration = Agent365SdkUserAgentConfiguration.Instance }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages, ToolOptions toolOptions, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext, nameof(turnContext));
            ArgumentNullException.ThrowIfNull(chatHistoryMessages, nameof(chatHistoryMessages));
            cancellationToken.ThrowIfCancellationRequested();

            // Extract required information from turn context
            var conversationId = turnContext.Activity?.Conversation?.Id ?? throw new InvalidOperationException("Conversation ID is required but not found in turn context");
            var messageId = turnContext.Activity?.Id ?? throw new InvalidOperationException("Message ID is required but not found in turn context");
            var userMessage = turnContext.Activity?.Text ?? throw new InvalidOperationException("User message is required but not found in turn context");

            // Get the endpoint URL
            var endpoint = Utility.GetChatHistoryEndpoint(this._configuration);

            this._logger.LogInformation($"Sending chat history to endpoint: {endpoint}");

            // Create the request payload
            var request = new ChatMessageRequest(conversationId, messageId, userMessage, chatHistoryMessages);

            try
            {
                var userAgentConfiguration = toolOptions?.UserAgentConfiguration ?? Agent365SdkUserAgentConfiguration.Instance;
                var httpClient = RuntimeUtility.GetDefaultHttpClient(httpClientFactory: this._httpClientFactory, userAgentConfiguration: userAgentConfiguration);

                var jsonContent = JsonSerializer.Serialize(request);
                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                this._logger.LogInformation("Successfully sent chat history to MCP platform");
                return OperationResult.Success;
            }
            catch (HttpRequestException httpEx)
            {
                this._logger.LogError(httpEx, "HTTP error sending chat history to '{Endpoint}': {Message}", endpoint, httpEx.Message);
                return OperationResult.Failed(new OperationError(httpEx));
            }
            catch (TaskCanceledException tcEx)
            {
                this._logger.LogError(tcEx, "Request timeout sending chat history to '{Endpoint}': {Message}", endpoint, tcEx.Message);
                return OperationResult.Failed(new OperationError(tcEx));
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to send chat history to '{Endpoint}': {Message}", endpoint, ex.Message);
                return OperationResult.Failed(new OperationError(ex));
            }
        }

        private async Task<List<MCPServerConfig>> GetMCPServerFromToolingGatewayAsync(
            string agentInstanceId, string authToken, ToolOptions toolOptions)
        {
            string configEndpoint = Utility.GetToolingGatewayForDigitalWorker(agentInstanceId, this._configuration);

            if (string.IsNullOrWhiteSpace(configEndpoint))
            {
                throw new InvalidOperationException("Configuration endpoint is not configured");
            }

            try
            {
                var userAgentConfiguration = toolOptions?.UserAgentConfiguration ?? Agent365SdkUserAgentConfiguration.Instance;
                var httpClient = RuntimeUtility.GetDefaultHttpClient(httpClientFactory: this._httpClientFactory, userAgentConfiguration: userAgentConfiguration);
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authToken);

                var response = await httpClient.GetStringAsync(configEndpoint);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Single parse approach
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(response, options);

                IEnumerable<JsonElement> serverElements = jsonDoc.ValueKind switch
                {
                    JsonValueKind.Array => jsonDoc.EnumerateArray(),
                    JsonValueKind.Object when jsonDoc.TryGetProperty("mcpServers", out var servers)
                                           && servers.ValueKind == JsonValueKind.Array
                        => servers.EnumerateArray(),
                    _ => throw new InvalidOperationException(
                        $"Unexpected JSON structure. Expected array or object with 'mcpServers' property, got {jsonDoc.ValueKind}")
                };

                return serverElements
                    .Select(ParseServerConfig)
                    .Where(config => config != null)
                    .ToList()!;
            }
            catch (HttpRequestException httpEx)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve configuration from '{configEndpoint}': {httpEx.Message}", httpEx);
            }
            catch (JsonException jsonEx)
            {
                throw new InvalidOperationException(
                    $"Failed to parse configuration response from '{configEndpoint}': {jsonEx.Message}", jsonEx);
            }
        }

        /// <summary>
        /// Parses a JSON element into an MCPServerConfig object.
        /// </summary>
        /// <param name="serverElement">The JSON element containing server configuration</param>
        /// <returns>MCPServerConfig object or null if parsing fails</returns>
        private static MCPServerConfig? ParseServerConfig(JsonElement serverElement)
        {
            try
            {
                string? name = null;
                string? endpoint = null;
                string? id = null;
                string? scope = null;
                string? audience = null;
                string? publisher = null;

                if (serverElement.TryGetProperty("mcpServerName", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }
                else if (serverElement.TryGetProperty("mcpServerUniqueName", out var mcpServerUniqueNameElement) &&
                    mcpServerUniqueNameElement.ValueKind == JsonValueKind.String)
                {
                    name = mcpServerUniqueNameElement.GetString();
                }

                if (serverElement.TryGetProperty("url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String)
                {
                    endpoint = urlElement.GetString();
                }

                if (serverElement.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                {
                    id = idElement.GetString();
                }
                if (serverElement.TryGetProperty("scope", out var scopeElement) &&
                    scopeElement.ValueKind == JsonValueKind.String)
                {
                    scope = scopeElement.GetString();
                }
                if (serverElement.TryGetProperty("audience", out var audienceElement) &&
                    audienceElement.ValueKind == JsonValueKind.String)
                {
                    audience = audienceElement.GetString();
                }
                if (serverElement.TryGetProperty("publisher", out var publisherElement) &&
                    publisherElement.ValueKind == JsonValueKind.String)
                {
                    publisher = publisherElement.GetString();
                }

                // Both Name and Endpoint are required
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(endpoint))
                {
                    return null;
                }

                return new MCPServerConfig
                {
                    mcpServerName = name,
                    url = endpoint,
                    id = id ?? string.Empty,
                    scope = scope ?? string.Empty,
                    audience = audience ?? string.Empty,
                    publisher = publisher ?? string.Empty
                };
            }
            catch (Exception)
            {
                // Return null if parsing fails for this individual server
                return null;
            }
        }

        /// <summary>
        /// Parses a JSON element into an MCPServerConfig object from manifest, constructing full URL.
        /// </summary>
        /// <param name="serverElement">The JSON element containing server configuration</param>
        /// <returns>MCPServerConfig object or null if parsing fails</returns>
        private MCPServerConfig? ParseServerConfigFromManifest(JsonElement serverElement)
        {
            try
            {
                string? name = null;
                string? endpoint = null;
                string? id = null;
                string? scope = null;
                string? audience = null;
                string? publisher = null;

                if (serverElement.TryGetProperty("mcpServerName", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }
                else if (serverElement.TryGetProperty("mcpServerUniqueName", out var mcpServerUniqueNameElement) &&
                    mcpServerUniqueNameElement.ValueKind == JsonValueKind.String)
                {
                    name = mcpServerUniqueNameElement.GetString();
                }

                if (serverElement.TryGetProperty("url", out var urlElement) &&
                    urlElement.ValueKind == JsonValueKind.String)
                {
                    endpoint = urlElement.GetString();
                }

                if (serverElement.TryGetProperty("id", out var idElement) &&
                    idElement.ValueKind == JsonValueKind.String)
                {
                    id = idElement.GetString();
                }
                if (serverElement.TryGetProperty("scope", out var scopeElement) &&
                    scopeElement.ValueKind == JsonValueKind.String)
                {
                    scope = scopeElement.GetString();
                }
                if (serverElement.TryGetProperty("audience", out var audienceElement) &&
                    audienceElement.ValueKind == JsonValueKind.String)
                {
                    audience = audienceElement.GetString();
                }
                if (serverElement.TryGetProperty("publisher", out var publisherElement) &&
                    publisherElement.ValueKind == JsonValueKind.String)
                {
                    publisher = publisherElement.GetString();
                }

                // Parse transport type
                McpTransportType transportType = McpTransportType.Sse;
                if (serverElement.TryGetProperty("transportType", out var transportTypeElement) &&
                    transportTypeElement.ValueKind == JsonValueKind.String)
                {
                    var transportTypeStr = transportTypeElement.GetString();
                    if (!string.IsNullOrEmpty(transportTypeStr) &&
                        Enum.TryParse<McpTransportType>(transportTypeStr, ignoreCase: true, out var parsedType))
                    {
                        transportType = parsedType;
                    }
                }

                // Parse WNS config if present
                WnsTransportConfig? wnsConfig = null;
                if (serverElement.TryGetProperty("wnsConfig", out var wnsConfigElement) &&
                    wnsConfigElement.ValueKind == JsonValueKind.Object)
                {
                    wnsConfig = ParseWnsConfig(wnsConfigElement);
                }

                // Both Name and ServerName are required
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                // Construct full URL if not provided in manifest (not required for WNS transport)
                var fullUrl = endpoint ?? (transportType == McpTransportType.Wns ? string.Empty : Utility.BuildMcpServerUrl(name, this._configuration));

                return new MCPServerConfig
                {
                    mcpServerName = name,
                    url = fullUrl,
                    id = id ?? string.Empty,
                    scope = scope ?? string.Empty,
                    audience = audience ?? string.Empty,
                    publisher = publisher ?? string.Empty,
                    transportType = transportType,
                    wnsConfig = wnsConfig
                };
            }
            catch (Exception)
            {
                // Return null if parsing fails for this individual server
                return null;
            }
        }

        /// <summary>
        /// Parses WNS configuration from a JSON element.
        /// </summary>
        private WnsTransportConfig? ParseWnsConfig(JsonElement wnsConfigElement)
        {
            string? clientName = null;
            string? proxyBaseUrl = null;
            string? channelUri = null;
            string? localServerId = null;
            int connectionTimeoutSeconds = 30;

            if (wnsConfigElement.TryGetProperty("clientName", out var clientNameElement) &&
                clientNameElement.ValueKind == JsonValueKind.String)
            {
                clientName = clientNameElement.GetString();
            }

            if (wnsConfigElement.TryGetProperty("proxyBaseUrl", out var proxyBaseUrlElement) &&
                proxyBaseUrlElement.ValueKind == JsonValueKind.String)
            {
                proxyBaseUrl = proxyBaseUrlElement.GetString();
            }

            if (wnsConfigElement.TryGetProperty("channelUri", out var channelUriElement) &&
                channelUriElement.ValueKind == JsonValueKind.String)
            {
                channelUri = channelUriElement.GetString();
            }

            if (wnsConfigElement.TryGetProperty("localServerId", out var localServerIdElement) &&
                localServerIdElement.ValueKind == JsonValueKind.String)
            {
                localServerId = localServerIdElement.GetString();
            }

            if (wnsConfigElement.TryGetProperty("connectionTimeoutSeconds", out var timeoutElement) &&
                timeoutElement.ValueKind == JsonValueKind.Number)
            {
                connectionTimeoutSeconds = timeoutElement.GetInt32();
            }

            // Client name is required for WNS config
            if (string.IsNullOrWhiteSpace(clientName))
            {
                return null;
            }

            return new WnsTransportConfig
            {
                clientName = clientName,
                proxyBaseUrl = proxyBaseUrl,
                channelUri = channelUri,
                localServerId = localServerId,
                connectionTimeoutSeconds = connectionTimeoutSeconds
            };
        }

        /// <summary>
        /// Reads MCP server configurations from ToolingManifest.json in the application's content root.
        /// The file should be located at: [ProjectRoot]/ToolingManifest.json
        ///
        /// Example ToolingManifest.json:
        /// {
        ///   "mcpServers": [
        ///     {
        ///       "mcpServerName": "mailMCPServer",
        ///       "url": "mcp_MailTools"
        ///     },
        ///     {
        ///       "mcpServerName": "sharePointMCPServer",
        ///       "url": "mcp_SharePointTools"
        ///     }
        ///   ]
        /// }
        /// </summary>
        /// <returns>List of MCP server configurations</returns>
        private List<MCPServerConfig> GetMCPServersFromManifest()
        {
            var mcpServers = new List<MCPServerConfig>();

            try
            {
                // Look for ToolingManifest.json in the application's base directory
                // This follows the pattern of how content files like appsettings.json are located
                var baseDirectory = AppContext.BaseDirectory;
                var manifestPath = Path.Combine(baseDirectory, "ToolingManifest.json");

                // If not found in base directory, try the current working directory
                if (!File.Exists(manifestPath))
                {
                    manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "ToolingManifest.json");
                }

                // If still not found, try looking in the entry assembly's directory
                if (!File.Exists(manifestPath))
                {
                    var entryAssembly = Assembly.GetEntryAssembly();
                    if (entryAssembly?.Location != null)
                    {
                        var assemblyDir = Path.GetDirectoryName(entryAssembly.Location);
                        if (!string.IsNullOrEmpty(assemblyDir))
                        {
                            manifestPath = Path.Combine(assemblyDir, "ToolingManifest.json");
                        }
                    }
                }

                if (File.Exists(manifestPath))
                {
                    this._logger.LogInformation($"Loading MCP servers from: {manifestPath}");

                    var jsonContent = File.ReadAllText(manifestPath);
                    var manifestData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    if (manifestData.TryGetProperty("mcpServers", out var serversElement))
                    {
                        this._logger.LogInformation("Found 'mcpServers' section in ToolingManifest.json");
                        if (serversElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var serverElement in serversElement.EnumerateArray())
                            {
                                var serverConfig = ParseServerConfigFromManifest(serverElement);
                                if (serverConfig != null)
                                {
                                    mcpServers.Add(serverConfig);
                                }
                            }
                        }
                    }

                    this._logger.LogInformation($"Loaded {mcpServers.Count} MCP server configurations");
                }
                else
                {
                    this._logger.LogInformation($"ToolingManifest.json not found. Expected location: {manifestPath}");
                    this._logger.LogInformation("Please ensure ToolingManifest.json exists in your project's output directory and is set to 'Copy to Output Directory'.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read MCP servers from ToolingManifest.json: {ex.Message}", ex);
            }

            return mcpServers;
        }

        /// <summary>
        /// Creates an MCP client with authentication handlers similar to your reference implementation
        /// </summary>
        private async Task<IMcpClient> CreateMcpClientWithAuthHandlers(ITurnContext turnContext, Uri endpoint, string authToken, ToolOptions toolOptions)
        {
            // Create HTTP client handler chain for MCP service authentication
            var httpClientHandler = new HttpClientHandler();

            // WARNING: Only use this in development/testing - never in production!
            // This bypasses SSL certificate validation
            var isDevScenario = IsDevScenario();
            if (isDevScenario)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                this._logger.LogInformation("WARNING: SSL certificate validation disabled for development!");
            }

            // Create a simple authentication handler that adds the bearer token
            var authHandler = new BearerTokenHandler(authToken)
            {
                InnerHandler = httpClientHandler
            };

            this._logger.LogInformation($"Configured authentication handler for MCP endpoint {endpoint}");

            var httpContextHeaderHandler = new HttpContextHeadersHandler(turnContext, this._logger, toolOptions)
            {
                InnerHandler = authHandler,
            };

            // Create logging handler (optional - for debugging HTTP requests)
            var loggingHandler = new HttpLoggingHandler(this._logger)
            {
                InnerHandler = httpContextHeaderHandler
            };

            // Setup SSE client transport options without manual token management
            var options = new SseClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.AutoDetect,
            };

            // Create HTTP client with the authentication handler chain
            var httpClient = new HttpClient(loggingHandler);

            var clientTransport = new SseClientTransport(options, httpClient);

            try
            {
                return await McpClientFactory.CreateAsync(clientTransport, loggerFactory: this._loggerFactory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create MCP client for endpoint '{endpoint}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a WNS-based MCP client for communicating with local Windows desktop MCP servers.
        /// </summary>
        /// <param name="serverConfig">The MCP server configuration containing WNS settings.</param>
        /// <returns>An IMcpClient that communicates via WNS.</returns>
        private async Task<IMcpClient> CreateWnsMcpClientAsync(MCPServerConfig serverConfig)
        {
            // Validate WNS configuration
            var wnsConfig = serverConfig.wnsConfig
                ?? throw new ArgumentException("WNS configuration is required for WNS transport. Set wnsConfig in the server configuration.", nameof(serverConfig));

            if (string.IsNullOrWhiteSpace(wnsConfig.clientName))
            {
                throw new ArgumentException("WNS client name is required for WNS transport", nameof(serverConfig));
            }

            // The proxy base URL should be set in WNS config or fall back to server URL
            var proxyBaseUrl = wnsConfig.proxyBaseUrl ?? serverConfig.url
                ?? throw new InvalidOperationException("WNS proxy base URL is required. Set proxyBaseUrl in wnsConfig or url in server config.");

            this._logger.LogInformation($"Creating WNS MCP client for: {serverConfig.mcpServerName} (client: {wnsConfig.clientName})");

            var transportOptions = new WnsClientTransportOptions
            {
                ClientName = wnsConfig.clientName,
                ProxyBaseUrl = proxyBaseUrl,
                ConnectionTimeoutSeconds = wnsConfig.connectionTimeoutSeconds > 0 ? wnsConfig.connectionTimeoutSeconds : 30,
                LocalServerId = wnsConfig.localServerId,
                AgentAppId = wnsConfig.agentAppId
            };

            // Create HTTP client for WNS proxy communication
            var httpClient = this._httpClientFactory.CreateClient("WnsMcpClient");

            var logger = this._loggerFactory?.CreateLogger<WnsClientTransport>();
            var wnsTransport = new WnsClientTransport(transportOptions, httpClient, logger);

            try
            {
                return await McpClientFactory.CreateAsync(wnsTransport, loggerFactory: this._loggerFactory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create WNS MCP client for '{serverConfig.mcpServerName}': {ex.Message}", ex);
            }
        }

        private bool IsDevScenario()
        {
            // Determine environment from configuration (environment variables, appsettings.json, etc.), default to 'Development' if not set
            var environment = this._configuration["ASPNETCORE_ENVIRONMENT"] ??
                             this._configuration["DOTNET_ENVIRONMENT"] ??
                             "Development";

            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<List<MCPServerConfig>> ListToolServersWithLocalDiscoveryAsync(
            string agentInstanceId,
            string authToken,
            ToolOptions toolOptions,
            string? localClientName,
            CancellationToken cancellationToken = default)
        {
            var allServers = new List<MCPServerConfig>();

            // 1. Get cloud servers from ATG (or manifest in dev)
            try
            {
                var cloudServers = await ListToolServersAsync(agentInstanceId, authToken, toolOptions);
                
                // Filter to only include SSE (cloud) servers from the manifest
                var sseServers = cloudServers.Where(s => s.transportType == McpTransportType.Sse).ToList();
                allServers.AddRange(sseServers);
                
                this._logger.LogInformation("[Discovery] Found {Count} cloud MCP servers", sseServers.Count);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "[Discovery] Failed to get cloud MCP servers");
            }

            // 2. Discover local servers from Windows desktop via WNS (if client name provided)
            // The desktop client receives a "list_servers" notification, runs `odr mcp list`,
            // and returns the results via HTTP callback.
            if (!string.IsNullOrEmpty(localClientName))
            {
                var proxyBaseUrl = this._configuration["LocalMcp:BaseUrl"];
                if (!string.IsNullOrEmpty(proxyBaseUrl))
                {
                    try
                    {
                        // First, check Intune compliance before discovering local servers
                        this._logger.LogInformation("[Discovery] Checking Intune compliance for client '{ClientName}'", localClientName);
                        var intuneResult = await CheckIntuneComplianceAsync(localClientName, proxyBaseUrl, 30, cancellationToken);

                        if (!intuneResult.IsIntuneManaged)
                        {
                            this._logger.LogWarning("[Discovery] Device for client '{ClientName}' is NOT Intune managed. Skipping local server discovery.", localClientName);
                            this._logger.LogWarning("[Discovery] Intune check result: IsAzureAdJoined={IsAzureAdJoined}, Error={Error}",
                                intuneResult.IsAzureAdJoined, intuneResult.ErrorMessage ?? "none");
                        }
                        else
                        {
                            this._logger.LogInformation("[Discovery] Device for client '{ClientName}' is Intune managed. Proceeding with local server discovery.", localClientName);
                            var localServers = await DiscoverLocalServersAsync(localClientName, proxyBaseUrl, cancellationToken);
                            allServers.AddRange(localServers);
                            this._logger.LogInformation("[Discovery] Found {Count} local MCP servers from desktop client '{ClientName}'",
                                localServers.Count, localClientName);
                        }
                    }
                    catch (LocalMcpDesktopRegistrationRequiredException)
                    {
                        // Let registration required exceptions propagate to the agent
                        throw;
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogWarning(ex, "[Discovery] Failed to discover local MCP servers from desktop client '{ClientName}'", localClientName);
                    }
                }
                else
                {
                    this._logger.LogDebug("[Discovery] LocalMcp:BaseUrl not configured, skipping local server discovery");
                }
            }

            this._logger.LogInformation("[Discovery] Total MCP servers available: {Count}", allServers.Count);
            return allServers;
        }

        /// <inheritdoc/>
        public async Task<LocalDiscoveryResult> ListToolServersWithUserDiscoveryAsync(
            string agentInstanceId,
            string authToken,
            ToolOptions toolOptions,
            string userIdentifier,
            CancellationToken cancellationToken = default)
        {
            var result = new LocalDiscoveryResult
            {
                UserIdentifier = userIdentifier,
                Servers = new List<MCPServerConfig>()
            };

            // 1. Get cloud servers from ATG (always available)
            try
            {
                var cloudServers = await ListToolServersAsync(agentInstanceId, authToken, toolOptions);
                var sseServers = cloudServers.Where(s => s.transportType == McpTransportType.Sse).ToList();
                result.Servers.AddRange(sseServers);
                this._logger.LogInformation("[UserDiscovery] Found {Count} cloud MCP servers", sseServers.Count);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "[UserDiscovery] Failed to get cloud MCP servers");
            }

            // 2. Look up registered desktops by user identity
            var proxyBaseUrl = this._configuration["LocalMcp:BaseUrl"];
            if (string.IsNullOrEmpty(proxyBaseUrl))
            {
                this._logger.LogDebug("[UserDiscovery] LocalMcp:BaseUrl not configured, skipping local server discovery");
                return result;
            }

            try
            {
                var httpClient = this._httpClientFactory.CreateClient("LocalMcpDiscovery");
                var encodedUserIdentifier = System.Net.WebUtility.UrlEncode(userIdentifier);
                
                using var response = await httpClient.GetAsync(
                    $"{proxyBaseUrl}/api/channels/by-user/{encodedUserIdentifier}",
                    cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                this._logger.LogDebug("[UserDiscovery] User lookup response: {StatusCode} - {Body}", 
                    response.StatusCode, responseBody);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No desktops registered for this user
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    if (errorJson.TryGetProperty("registrationProtocolUrl", out var regUrlProp))
                    {
                        result.RequiresRegistration = true;
                        result.RegistrationProtocolUrl = regUrlProp.GetString();
                        result.ErrorMessage = $"No desktops registered for user '{userIdentifier}'.";
                        
                        this._logger.LogWarning("[UserDiscovery] No desktops registered for user '{UserIdentifier}'. Registration URL: {RegistrationUrl}",
                            userIdentifier, result.RegistrationProtocolUrl);
                        
                        throw new LocalMcpDesktopRegistrationRequiredException(
                            userIdentifier,
                            result.RegistrationProtocolUrl ?? string.Empty,
                            result.ErrorMessage);
                    }
                    return result;
                }

                if (!response.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Failed to look up user's desktops: {response.StatusCode}";
                    this._logger.LogWarning("[UserDiscovery] {Error}", result.ErrorMessage);
                    return result;
                }

                // Parse the list of registered desktops
                var userDesktopsJson = JsonSerializer.Deserialize<JsonElement>(responseBody);
                if (userDesktopsJson.TryGetProperty("clients", out var clientsArray))
                {
                    foreach (var clientJson in clientsArray.EnumerateArray())
                    {
                        var desktop = new DesktopClientInfo
                        {
                            ClientName = clientJson.GetProperty("clientName").GetString() ?? string.Empty,
                            MachineName = clientJson.GetProperty("machineName").GetString() ?? string.Empty,
                            RegisteredAt = clientJson.TryGetProperty("registeredAt", out var regAt) 
                                ? regAt.GetDateTime() : DateTime.MinValue,
                            LastSeen = clientJson.TryGetProperty("lastSeen", out var lastSeen) 
                                ? lastSeen.GetDateTime() : DateTime.MinValue
                        };
                        result.AllRegisteredDesktops.Add(desktop);
                    }
                }

                if (result.AllRegisteredDesktops.Count == 0)
                {
                    result.RequiresRegistration = true;
                    result.RegistrationProtocolUrl = $"locaproto:?action=register&callback={proxyBaseUrl}/api/channels/register&user={encodedUserIdentifier}";
                    result.ErrorMessage = $"No desktops registered for user '{userIdentifier}'.";
                    
                    throw new LocalMcpDesktopRegistrationRequiredException(
                        userIdentifier,
                        result.RegistrationProtocolUrl,
                        result.ErrorMessage);
                }

                // 3. Pick the most recently active desktop
                result.ActiveDesktop = result.AllRegisteredDesktops
                    .OrderByDescending(d => d.LastSeen)
                    .First();

                this._logger.LogInformation("[UserDiscovery] User '{UserIdentifier}' has {Count} registered desktop(s). Using '{ActiveDesktop}' (last seen: {LastSeen})",
                    userIdentifier, result.AllRegisteredDesktops.Count, result.ActiveDesktop.ClientName, result.ActiveDesktop.LastSeen);

                if (result.AllRegisteredDesktops.Count > 1)
                {
                    this._logger.LogInformation("[UserDiscovery] Other registered desktops: {Others}",
                        string.Join(", ", result.AllRegisteredDesktops.Where(d => d.ClientName != result.ActiveDesktop.ClientName).Select(d => d.ClientName)));
                }

                // 4. Now perform Intune check and discovery using the selected desktop
                var intuneResult = await CheckIntuneComplianceAsync(result.ActiveDesktop.ClientName, proxyBaseUrl, 30, cancellationToken);

                if (!intuneResult.IsIntuneManaged)
                {
                    this._logger.LogWarning("[UserDiscovery] Desktop '{ClientName}' is NOT Intune managed. Skipping local server discovery.", 
                        result.ActiveDesktop.ClientName);
                }
                else
                {
                    this._logger.LogInformation("[UserDiscovery] Desktop '{ClientName}' is Intune managed. Discovering local servers...", 
                        result.ActiveDesktop.ClientName);
                    
                    var localServers = await DiscoverLocalServersAsync(result.ActiveDesktop.ClientName, proxyBaseUrl, cancellationToken);
                    result.Servers.AddRange(localServers);
                    
                    this._logger.LogInformation("[UserDiscovery] Found {Count} local MCP servers from '{ClientName}'",
                        localServers.Count, result.ActiveDesktop.ClientName);
                }
            }
            catch (LocalMcpDesktopRegistrationRequiredException)
            {
                throw;
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "[UserDiscovery] Failed to discover desktops for user '{UserIdentifier}'", userIdentifier);
                result.ErrorMessage = ex.Message;
            }

            this._logger.LogInformation("[UserDiscovery] Total MCP servers available: {Count}", result.Servers.Count);
            return result;
        }

        /// <summary>
        /// Gets local MCP servers from configuration (appsettings.json LocalMcp:Servers section).
        /// This is a fallback configuration-based approach when dynamic discovery is not available.
        /// </summary>
        private List<MCPServerConfig> GetLocalServersFromConfiguration(string clientName)
        {
            var configs = new List<MCPServerConfig>();

            var proxyBaseUrl = this._configuration["LocalMcp:BaseUrl"];
            var connectionTimeout = this._configuration.GetValue("LocalMcp:ConnectionTimeoutSeconds", 30);

            if (string.IsNullOrEmpty(proxyBaseUrl))
            {
                this._logger.LogWarning("[Discovery] LocalMcp:BaseUrl not configured");
                return configs;
            }

            // Read servers from LocalMcp:Servers array
            var serversSection = this._configuration.GetSection("LocalMcp:Servers");
            if (!serversSection.Exists())
            {
                return configs;
            }

            foreach (var serverSection in serversSection.GetChildren())
            {
                var serverId = serverSection["ServerId"];
                var serverName = serverSection["Name"] ?? serverId;
                var description = serverSection["Description"] ?? string.Empty;

                if (string.IsNullOrEmpty(serverId))
                {
                    this._logger.LogWarning("[Discovery] Skipping local server with empty ServerId");
                    continue;
                }

                var config = new MCPServerConfig
                {
                    mcpServerName = serverName ?? serverId,
                    id = serverId,
                    url = string.Empty, // Not needed for WNS transport
                    scope = string.Empty,
                    audience = string.Empty,
                    publisher = "Local",
                    transportType = McpTransportType.Wns,
                    wnsConfig = new WnsTransportConfig
                    {
                        clientName = clientName,
                        proxyBaseUrl = proxyBaseUrl,
                        localServerId = serverId,
                        connectionTimeoutSeconds = connectionTimeout
                    }
                };

                configs.Add(config);
                this._logger.LogDebug("[Discovery] Configured local server: {ServerName} ({ServerId})", serverName, serverId);
            }

            return configs;
        }

        /// <summary>
        /// Discovers local MCP servers from a Windows desktop client via WNS.
        /// 
        /// Flow:
        /// 1. SDK sends WNS notification with type="list_servers" (no serverId)
        /// 2. Desktop client receives notification and runs `odr mcp list` locally
        /// 3. Desktop client posts results to callback URL: POST /api/discovery/{requestId}/servers
        /// 4. SDK polls for results at: GET /api/discovery/{requestId}/servers
        /// 
        /// This is different from MCP tool invocation which sends a serverId to start a specific server.
        /// </summary>
        private async Task<List<MCPServerConfig>> DiscoverLocalServersAsync(
            string clientName,
            string proxyBaseUrl,
            CancellationToken cancellationToken)
        {
            this._logger.LogInformation("[Discovery] Requesting local MCP server list from desktop client '{ClientName}'", clientName);

            var httpClient = this._httpClientFactory.CreateClient("LocalMcpDiscovery");
            var requestId = Guid.NewGuid().ToString();

            try
            {
                // Step 1: Send a "list_servers" WNS notification (NOT an MCP request)
                // This is a special discovery request - the desktop client should NOT start an MCP server
                // Instead, it should run `odr mcp list` and POST the results back
                var notifyRequest = new
                {
                    type = "list_servers",
                    requestId = requestId,
                    callbackUrl = $"{proxyBaseUrl}/api/discovery/{requestId}/servers"
                };

                var notifyRequestJson = JsonSerializer.Serialize(notifyRequest);
                this._logger.LogDebug("[Discovery] Sending discovery notification: {Request}", notifyRequestJson);

                // Send notification via the proxy's notify endpoint
                var notifyResponse = await httpClient.PostAsync(
                    $"{proxyBaseUrl}/api/notify/{clientName}",
                    new StringContent(notifyRequestJson, Encoding.UTF8, "application/json"),
                    cancellationToken);

                // Check if desktop client is not registered (404 with CLIENT_NOT_REGISTERED)
                if (notifyResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorContent = await notifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    
                    if (errorContent.TryGetProperty("error", out var errorProp) && 
                        errorProp.GetString() == "CLIENT_NOT_REGISTERED" &&
                        errorContent.TryGetProperty("registrationProtocolUrl", out var regUrlProp))
                    {
                        var registrationUrl = regUrlProp.GetString() ?? string.Empty;
                        this._logger.LogWarning("[Discovery] Desktop client '{ClientName}' is not registered. Registration URL: {RegistrationUrl}", 
                            clientName, registrationUrl);
                        
                        throw new LocalMcpDesktopRegistrationRequiredException(
                            clientName,
                            registrationUrl,
                            $"Desktop client '{clientName}' is not registered. Please register your desktop to enable local file access.");
                    }
                }

                notifyResponse.EnsureSuccessStatusCode();
                this._logger.LogDebug("[Discovery] Notification sent, requestId: {RequestId}", requestId);

                // Step 2: Poll for the discovery results
                // The desktop client will POST the `odr mcp list` results to the callback URL
                var timeout = TimeSpan.FromSeconds(this._configuration.GetValue("LocalMcp:ConnectionTimeoutSeconds", 30));
                var servers = await PollForDiscoveryResultsAsync(httpClient, proxyBaseUrl, requestId, timeout, cancellationToken);

                return ConvertLocalServersToConfig(servers, clientName, proxyBaseUrl);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "[Discovery] Failed to discover local MCP servers from '{ClientName}'", clientName);
                throw;
            }
        }

        /// <summary>
        /// Polls the proxy for discovery results posted by the desktop client.
        /// </summary>
        private async Task<List<LocalMcpServerInfo>> PollForDiscoveryResultsAsync(
            HttpClient httpClient,
            string proxyBaseUrl,
            string requestId,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(1);

            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    var response = await httpClient.GetAsync(
                        $"{proxyBaseUrl}/api/discovery/{requestId}/servers",
                        cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                        
                        // Check if results are ready
                        if (result.TryGetProperty("status", out var statusElement))
                        {
                            var status = statusElement.GetString();
                            if (status == "completed")
                            {
                                if (result.TryGetProperty("servers", out var serversElement))
                                {
                                    var servers = JsonSerializer.Deserialize<List<LocalMcpServerInfo>>(serversElement.GetRawText())
                                        ?? new List<LocalMcpServerInfo>();
                                    
                                    this._logger.LogInformation("[Discovery] Received {Count} servers from desktop", servers.Count);
                                    return servers;
                                }
                            }
                            else if (status == "error")
                            {
                                var errorMessage = result.TryGetProperty("error", out var errorElement) 
                                    ? errorElement.GetString() 
                                    : "Unknown error";
                                throw new InvalidOperationException($"Desktop discovery failed: {errorMessage}");
                            }
                            // status == "pending" - continue polling
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Request not yet received by proxy, continue polling
                        this._logger.LogDebug("[Discovery] Results not ready yet, polling...");
                    }
                }
                catch (HttpRequestException ex)
                {
                    this._logger.LogDebug(ex, "[Discovery] Poll request failed, will retry");
                }

                await Task.Delay(pollInterval, cancellationToken);
            }

            throw new TimeoutException($"Discovery request {requestId} timed out after {timeout.TotalSeconds}s");
        }

        /// <summary>
        /// Converts local MCP server info (from odr mcp list) to MCPServerConfig objects.
        /// </summary>
        private List<MCPServerConfig> ConvertLocalServersToConfig(
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
                    this._logger.LogWarning("[Discovery] Skipping local server '{Name}' with no package identifier", server.Name);
                    continue;
                }

                // Use direct properties from the odr mcp list response
                var serverName = server.Name ?? serverId;
                var description = server.Description ?? string.Empty;

                // Extract static tools from the discovery response (avoids needing tools/list call later)
                var staticTools = server.GetStaticToolsList();
                if (staticTools != null)
                {
                    this._logger.LogDebug("[Discovery] Server '{ServerName}' has static tools list with {Count} tools",
                        serverName, staticTools.Value.GetArrayLength());
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
                        connectionTimeoutSeconds = this._configuration.GetValue("LocalMcp:ConnectionTimeoutSeconds", 30)
                    }
                };

                configs.Add(config);
                this._logger.LogDebug("[Discovery] Converted local server: {ServerName} ({ServerId}), hasStaticTools: {HasStatic}",
                    serverName, serverId, config.HasStaticTools);
            }

            return configs;
        }

        /// <summary>
        /// Waits for a desktop client to connect to a session.
        /// </summary>
        private async Task<bool> WaitForDesktopConnectionAsync(
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
                    this._logger.LogDebug(ex, "[Discovery] Connection check failed");
                }

                await Task.Delay(1000, cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// Result of an Intune compliance check for a Windows device.
        /// </summary>
        private class IntuneComplianceCheckResult
        {
            public bool IsIntuneManaged { get; set; }
            public bool IsAzureAdJoined { get; set; }
            public string? TenantId { get; set; }
            public string? DeviceId { get; set; }
            public string? MachineName { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Checks if a Windows desktop client is Intune managed before allowing local server discovery.
        /// This is a security requirement to ensure only managed devices can expose local MCP servers.
        /// </summary>
        private async Task<IntuneComplianceCheckResult> CheckIntuneComplianceAsync(
            string clientName,
            string proxyBaseUrl,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            this._logger.LogInformation("[Intune] Checking Intune compliance for client '{ClientName}'", clientName);

            var httpClient = this._httpClientFactory.CreateClient("LocalMcpDiscovery");

            try
            {
                // Step 1: Send an Intune check request via the WNS proxy
                using var initiateResponse = await httpClient.PostAsync(
                    $"{proxyBaseUrl}/api/intune-check/{clientName}",
                    null,
                    cancellationToken);

                if (!initiateResponse.IsSuccessStatusCode)
                {
                    var errorBody = await initiateResponse.Content.ReadAsStringAsync(cancellationToken);
                    this._logger.LogWarning("[Intune] Failed to initiate Intune check: {StatusCode} - {Error}",
                        initiateResponse.StatusCode, errorBody);
                    
                    // Check if the error is because the client is not registered
                    if (initiateResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Try to parse the error to see if it contains registration info
                        try
                        {
                            var errorJson = JsonSerializer.Deserialize<JsonElement>(errorBody);
                            if (errorJson.TryGetProperty("error", out var errorProp) && 
                                errorProp.GetString() == "CLIENT_NOT_REGISTERED" &&
                                errorJson.TryGetProperty("registrationProtocolUrl", out var regUrlProp))
                            {
                                var registrationUrl = regUrlProp.GetString() ?? string.Empty;
                                this._logger.LogWarning("[Intune] Desktop client '{ClientName}' is not registered. Registration URL: {RegistrationUrl}", 
                                    clientName, registrationUrl);
                                
                                throw new LocalMcpDesktopRegistrationRequiredException(
                                    clientName,
                                    registrationUrl,
                                    $"Desktop client '{clientName}' is not registered. Please register your desktop to enable local file access.");
                            }
                            
                            // Check for simple "Client not found" message - generate registration URL
                            if (errorJson.TryGetProperty("message", out var msgProp) && 
                                msgProp.GetString()?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var registrationUrl = $"locaproto:?action=register&callback={proxyBaseUrl}/api/channels/register";
                                this._logger.LogWarning("[Intune] Desktop client '{ClientName}' not found. Registration URL: {RegistrationUrl}", 
                                    clientName, registrationUrl);
                                
                                throw new LocalMcpDesktopRegistrationRequiredException(
                                    clientName,
                                    registrationUrl,
                                    $"Desktop client '{clientName}' is not registered. Please register your desktop to enable local file access.");
                            }
                        }
                        catch (JsonException)
                        {
                            // Not JSON, continue with default behavior
                        }
                    }
                    
                    return new IntuneComplianceCheckResult
                    {
                        IsIntuneManaged = false,
                        ErrorMessage = $"Failed to initiate Intune check: {initiateResponse.StatusCode}"
                    };
                }

                var initiateResult = await initiateResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var requestId = initiateResult.GetProperty("requestId").GetString()
                    ?? throw new InvalidOperationException("Failed to get requestId from Intune check response");

                this._logger.LogDebug("[Intune] Intune check initiated with requestId: {RequestId}", requestId);

                // Step 2: Poll for the Intune status result
                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                var startTime = DateTime.UtcNow;

                while (DateTime.UtcNow - startTime < timeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var pollResponse = await httpClient.GetAsync(
                        $"{proxyBaseUrl}/api/intune-status/{requestId}",
                        cancellationToken);

                    if (pollResponse.IsSuccessStatusCode)
                    {
                        var statusResult = await pollResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

                        if (statusResult.TryGetProperty("status", out var statusElement))
                        {
                            var status = statusElement.GetString();

                            if (status == "completed")
                            {
                                var result = new IntuneComplianceCheckResult
                                {
                                    IsIntuneManaged = statusResult.TryGetProperty("isIntuneManaged", out var im) && im.GetBoolean(),
                                    IsAzureAdJoined = statusResult.TryGetProperty("isAzureAdJoined", out var aad) && aad.GetBoolean(),
                                    TenantId = statusResult.TryGetProperty("tenantId", out var tid) ? tid.GetString() : null,
                                    DeviceId = statusResult.TryGetProperty("deviceId", out var did) ? did.GetString() : null,
                                    MachineName = statusResult.TryGetProperty("machineName", out var mn) ? mn.GetString() : null
                                };

                                this._logger.LogInformation("[Intune] Intune check completed: IsIntuneManaged={IsManaged}, IsAzureAdJoined={IsAadJoined}",
                                    result.IsIntuneManaged, result.IsAzureAdJoined);
                                return result;
                            }
                        }
                    }

                    await Task.Delay(500, cancellationToken);
                }

                this._logger.LogWarning("[Intune] Intune check timed out after {Timeout}s", timeoutSeconds);
                return new IntuneComplianceCheckResult
                {
                    IsIntuneManaged = false,
                    ErrorMessage = $"Intune check timed out after {timeoutSeconds} seconds"
                };
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "[Intune] Failed to check Intune compliance for '{ClientName}'", clientName);
                return new IntuneComplianceCheckResult
                {
                    IsIntuneManaged = false,
                    ErrorMessage = $"Exception: {ex.Message}"
                };
            }
        }
    }
}
