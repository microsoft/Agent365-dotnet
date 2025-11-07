// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.A365.Tooling.Handlers;
    using Microsoft.Agents.Builder;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Client;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides services for managing MCP server configurations.
    /// </summary>
    public class McpToolServerConfigurationService : IMcpToolServerConfigurationService
    {
        private readonly ILogger<IMcpToolServerConfigurationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolServerConfigurationService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for logging.</param>
        public McpToolServerConfigurationService(ILogger<IMcpToolServerConfigurationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets the list of MCP Servers that are configured for the agent.
        /// </summary>
        /// <param name="agentInstanceId">Agent Instance Id for the agent.</param>
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        public async Task<List<MCPServerConfig>> ListToolServers(string agentInstanceId, string environmentId, string authToken)
        {
            return IsDevScenario() ? GetMCPServersFromManifest(environmentId) : await GetMCPServerFromToolingGatewayAsync(agentInstanceId, environmentId, authToken);
        }

        /// <summary>
        /// Gets the MCP Client Tools from the specified MCP server.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="mCPServerConfig">The MCP server configuration.</param>
        /// <param name="environmentId">The environment ID.</param>
        /// <param name="authToken">The authentication token.</param>
        /// <returns>MCP Client Tools</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<IList<McpClientTool>> GetMcpClientTools(ITurnContext turnContext, MCPServerConfig mCPServerConfig, string environmentId, string authToken)
        {
            try
            {
                // Validate the server name
                if (string.IsNullOrWhiteSpace(mCPServerConfig.mcpServerName))
                {
                    throw new ArgumentException("MCP Server name cannot be null or empty", nameof(mCPServerConfig.mcpServerName));
                }

                this._logger.LogInformation($"Creating custom MCP client for: {mCPServerConfig.mcpServerName} at {mCPServerConfig.url}");

                // Use custom HTTP-based implementation since MCP client library doesn't work
                var mcpClient = await CreateMcpClientWithAuthHandlers(turnContext, new Uri(mCPServerConfig.url), mCPServerConfig.mcpServerName, environmentId, authToken);
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

        private static async Task<List<MCPServerConfig>> GetMCPServerFromToolingGatewayAsync(
            string agentInstanceId, string environmentId, string authToken)
        {
            string configEndpoint = Utility.GetToolingGatewayForDigitalWorker(agentInstanceId);

            if (string.IsNullOrWhiteSpace(configEndpoint))
            {
                throw new InvalidOperationException("Configuration endpoint is not configured");
            }

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", authToken);
                if (Utility.UseEnvironmentId())
                {
                    httpClient.DefaultRequestHeaders.Add("x-ms-environment-id", environmentId);
                }

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
        /// <param name="environmentId">Environment ID to construct full URL</param>
        /// <returns>MCPServerConfig object or null if parsing fails</returns>
        private static MCPServerConfig? ParseServerConfigFromManifest(JsonElement serverElement, string environmentId)
        {
            try
            {
                string? name = null;
                string? id = null;
                string? scope = null;
                string? audience = null;
                string? publisher = null;

                if (serverElement.TryGetProperty("mcpServerName", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
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

                // Construct full URL using environment utilities
                var fullUrl = Utility.BuildMcpServerUrl(environmentId, name);

                return new MCPServerConfig
                {
                    mcpServerName = name,
                    url = fullUrl,
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
        /// <param name="environmentId">Environment ID to construct full URLs.</param>
        /// <returns>List of MCP server configurations</returns>
        private List<MCPServerConfig> GetMCPServersFromManifest(string environmentId)
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
                                var serverConfig = ParseServerConfigFromManifest(serverElement, environmentId);
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
        private async Task<IMcpClient> CreateMcpClientWithAuthHandlers(ITurnContext turnContext, Uri endpoint, string clientName, string environmentId, string authToken)
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

            var httpContextHeaderHandler = new HttpContextHeadersHandler(turnContext)
            {
                InnerHandler = authHandler
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
            httpClient.DefaultRequestHeaders.Add(Constants.Headers.EnvironmentId, environmentId);

            var clientTransport = new SseClientTransport(options, httpClient);

            try
            {
                return await McpClientFactory.CreateAsync(clientTransport);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create MCP client for endpoint '{endpoint}': {ex.Message}", ex);
            }
        }

        private static bool IsDevScenario()
        {
            // Check environment variable first, default to dev if not set
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                             Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                             "Development";

            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
        }
    }
}
