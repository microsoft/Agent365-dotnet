// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Tooling.Handlers;
    using Microsoft.Agents.A365.Tooling.Models;
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
    public partial class McpToolServerConfigurationService : IMcpToolServerConfigurationService
    {
        private readonly ILogger<IMcpToolServerConfigurationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;

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
        }

        /// <inheritdoc/>
        public virtual async Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken)
        {
            return await ListToolServersAsync(agentInstanceId, authToken, new ToolOptions());
        }

        /// <inheritdoc/>
        public virtual async Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken, ToolOptions toolOptions)
        {
            if (IsDevScenario())
            {
                // Dev manifests carry no connection metadata, so connection gating never applies.
                return GetMCPServersFromManifest();
            }

            McpDiscoveryResult discovery = await GetMCPServerFromToolingGatewayAsync(agentInstanceId, authToken, toolOptions);

            // Gate execution when configured MCP servers are not connection-ready. Runs before token
            // attachment because readiness is independent of tokens.
            EnforceConnectionReadiness(discovery);

            return discovery.Servers;
        }

        /// <summary>
        /// Gets the list of MCP servers and attaches per-audience Bearer tokens to each server's
        /// <see cref="MCPServerConfig.Headers"/> dictionary before returning.
        /// V1 servers share the ATG-scoped token; V2 servers receive audience-specific tokens.
        /// </summary>
        internal virtual async Task<List<MCPServerConfig>> ListToolServersWithTokensAsync(
            string agentInstanceId,
            string authToken,
            IMcpTokenProvider tokenProvider,
            ToolOptions toolOptions,
            CancellationToken cancellationToken = default)
        {
            var servers = await ListToolServersAsync(agentInstanceId, authToken, toolOptions).ConfigureAwait(false);
            await AttachPerAudienceTokensAsync(servers, tokenProvider, cancellationToken).ConfigureAwait(false);
            return servers;
        }

        /// <inheritdoc/>
        public virtual async Task<IList<McpClientTool>> GetMcpClientToolsAsync(
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

                // Prefer the per-server token injected by AttachPerAudienceTokensAsync (V2 path).
                // Fall back to the caller-supplied authToken for V1 servers and dev scenarios.
                var effectiveToken = ResolveEffectiveToken(mCPServerConfig, authToken);

                this._logger.LogInformation($"Creating custom MCP client for: {mCPServerConfig.mcpServerName} at {mCPServerConfig.url}");

                // Use custom HTTP-based implementation since MCP client library doesn't work
                var mcpClient = await CreateMcpClientWithAuthHandlers(turnContext, new Uri(mCPServerConfig.url), effectiveToken, toolOptions);
                var tools = await mcpClient.ListToolsAsync();

                this._logger.LogInformation($"Successfully retrieved {tools.Count} tools from {mCPServerConfig.mcpServerName}");

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

        private async Task<McpDiscoveryResult> GetMCPServerFromToolingGatewayAsync(
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

                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(response, options);

                return ParseGatewayResponse(jsonDoc);
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

                string? allConnectionsUrl = GetStringPropertyCaseInsensitive(serverElement, "allConnectionsUrl");
                string? missingConnectionsUrl = GetStringPropertyCaseInsensitive(serverElement, "missingConnectionsUrl");
                string? connectivityStatus = GetStringPropertyCaseInsensitive(serverElement, "connectivityStatus");
                if (IsReadyStatus(connectivityStatus))
                {
                    missingConnectionsUrl = null;
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
                    scope = scope,
                    audience = audience,
                    publisher = publisher,
                    allConnectionsUrl = allConnectionsUrl,
                    missingConnectionsUrl = missingConnectionsUrl,
                    connectivityStatus = connectivityStatus
                };
            }
            catch (Exception)
            {
                // Return null if parsing fails for this individual server
                return null;
            }
        }

        /// <summary>
        /// Raises <see cref="McpConnectionsRequiredException"/> when the aggregate connectivity status
        /// indicates that one or more required downstream connections are missing.
        /// </summary>
        /// <param name="discovery">The discovery result carrying aggregate and per-server status.</param>
        /// <remarks>
        /// Blocks only when the response-level connectivity status is present and not <c>Ready</c>
        /// (for example, <c>Pending</c>). Absent status (legacy raw-array gateway responses and
        /// dev-mode manifests) is always treated as ready, so those paths are never gated. The
        /// non-<c>Ready</c> check is intentionally defensive against any unexpected future status value.
        /// </remarks>
        internal void EnforceConnectionReadiness(McpDiscoveryResult discovery)
        {
            string? status = discovery.ConnectivityStatus;
            if (string.IsNullOrWhiteSpace(status) || IsReadyStatus(status))
            {
                return;
            }

            List<string> serverNames = discovery.Servers
                .Where(s => !string.IsNullOrWhiteSpace(s.connectivityStatus) && !IsReadyStatus(s.connectivityStatus))
                .Select(s => string.IsNullOrEmpty(s.mcpServerName) ? s.id : s.mcpServerName)
                .ToList();

            this._logger.LogInformation(
                "MCP connection gate blocking turn: connectivityStatus={ConnectivityStatus}, servers={ServerNames}",
                status,
                string.Join(", ", serverNames));

            throw new McpConnectionsRequiredException(discovery.MissingConnectionsUrl, status, serverNames);
        }

        /// <summary>
        /// Parses a tooling gateway response into an <see cref="McpDiscoveryResult"/>. Supports both the
        /// legacy bare-array shape (servers only, no connection metadata) and the wrapped object shape
        /// <c>{ "mcpServers": [...], allConnectionsUrl, missingConnectionsUrl, connectivityStatus }</c>.
        /// </summary>
        /// <param name="root">The root JSON element of the gateway response.</param>
        /// <returns>The parsed discovery result with servers and aggregate connection metadata.</returns>
        internal static McpDiscoveryResult ParseGatewayResponse(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                // Legacy bare-array: servers only, no aggregate connection metadata.
                List<MCPServerConfig> legacyServers = root.EnumerateArray()
                    .Select(ParseServerConfig)
                    .Where(config => config != null)
                    .ToList()!;
                return new McpDiscoveryResult(legacyServers);
            }

            if (root.ValueKind == JsonValueKind.Object &&
                TryGetPropertyCaseInsensitive(root, "mcpServers", out var serversElement) &&
                serversElement.ValueKind == JsonValueKind.Array)
            {
                List<MCPServerConfig> servers = serversElement.EnumerateArray()
                    .Select(ParseServerConfig)
                    .Where(config => config != null)
                    .ToList()!;

                string? connectivityStatus = GetStringPropertyCaseInsensitive(root, "connectivityStatus");
                string? allConnectionsUrl = GetStringPropertyCaseInsensitive(root, "allConnectionsUrl");
                string? missingConnectionsUrl = GetStringPropertyCaseInsensitive(root, "missingConnectionsUrl");
                if (IsReadyStatus(connectivityStatus))
                {
                    missingConnectionsUrl = null;
                }

                return new McpDiscoveryResult(servers, allConnectionsUrl, missingConnectionsUrl, connectivityStatus);
            }

            throw new InvalidOperationException(
                $"Unexpected JSON structure. Expected array or object with 'mcpServers' property, got {root.ValueKind}");
        }

        /// <summary>
        /// The connectivity status value indicating that all required connectors are already connected.
        /// </summary>
        private const string ReadyConnectivityStatus = "Ready";

        /// <summary>
        /// Determines whether a connectivity status value represents the ready state (case-insensitive).
        /// </summary>
        /// <param name="connectivityStatus">The connectivity status value to evaluate.</param>
        /// <returns><c>true</c> when the value equals <c>Ready</c> ignoring case and surrounding whitespace.</returns>
        private static bool IsReadyStatus(string? connectivityStatus) =>
            !string.IsNullOrWhiteSpace(connectivityStatus) &&
            connectivityStatus!.Trim().Equals(ReadyConnectivityStatus, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Attempts to read a property from a JSON object using a case-insensitive name match. The
        /// property name is trimmed to tolerate stray whitespace in the source schema.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property name to match (case-insensitive).</param>
        /// <param name="value">The matched property value, or default when not found.</param>
        /// <returns><c>true</c> when a matching property is found; otherwise <c>false</c>.</returns>
        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            value = default;
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name.Trim(), propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a string property from a JSON object using a case-insensitive name match, or null when
        /// the property is absent or not a string.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property name to match (case-insensitive).</param>
        /// <returns>The string value, or null when not present.</returns>
        private static string? GetStringPropertyCaseInsensitive(JsonElement element, string propertyName)
        {
            if (TryGetPropertyCaseInsensitive(element, propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
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

                // Both Name and ServerName are required
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                // Construct full URL if not provided in manifest
                var fullUrl = endpoint ?? Utility.BuildMcpServerUrl(name, this._configuration);

                return new MCPServerConfig
                {
                    mcpServerName = name,
                    url = fullUrl,
                    id = id ?? string.Empty,
                    scope = scope,
                    audience = audience,
                    publisher = publisher
                };
            }
            catch (Exception)
            {
                // Return null if parsing fails for this individual server
                return null;
            }
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

            var httpContextHeaderHandler = new HttpContextHeadersHandler(turnContext, this._logger, toolOptions, authToken)
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
        /// Attaches a per-audience Bearer token to each server's
        /// <see cref="MCPServerConfig.Headers"/> dictionary.
        /// Tokens are deduped by resolved scope before calling the provider, so V1 servers
        /// that share the ATG scope trigger exactly one exchange regardless of how many
        /// V1 servers are present.
        /// </summary>
        private async Task AttachPerAudienceTokensAsync(
            List<MCPServerConfig> servers,
            IMcpTokenProvider tokenProvider,
            CancellationToken cancellationToken)
        {
            // Pre-compute distinct scopes so we only call the provider once per unique scope.
            // Sequential acquisition avoids throttling the OBO endpoint.
            var tokenByScope = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var server in servers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scope = Utils.Utility.ResolveTokenScopeForServer(server, _configuration);
                if (!tokenByScope.TryGetValue(scope, out var token))
                {
                    token = await tokenProvider.GetTokenAsync(server, cancellationToken).ConfigureAwait(false);
                    tokenByScope[scope] = token;
                    _logger.LogDebug(
                        "Acquired token for scope '{Scope}' (server '{ServerName}')",
                        scope, server.mcpServerName);
                }

                server.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                server.Headers[Constants.Headers.Authorization] = $"{Constants.Headers.BearerPrefix} {token}";
            }
        }

        /// <summary>
        /// Extracts the effective raw token for MCP client authentication.
        /// If <paramref name="serverConfig"/> already carries an Authorization header
        /// (set by <see cref="AttachPerAudienceTokensAsync"/>), the token from that header
        /// is used. Otherwise the caller-supplied <paramref name="fallbackToken"/> is returned.
        /// </summary>
        internal static string ResolveEffectiveToken(MCPServerConfig serverConfig, string fallbackToken)
        {
            if (serverConfig.Headers is not null &&
                serverConfig.Headers.TryGetValue(Constants.Headers.Authorization, out var headerValue) &&
                !string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase)
                    ? headerValue.Substring(Constants.Headers.BearerPrefix.Length + 1)
                    : headerValue;
            }

            return fallbackToken;
        }

        private bool IsDevScenario() => Utility.IsDevScenario(_configuration);
    }
}
