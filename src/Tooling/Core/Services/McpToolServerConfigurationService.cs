// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;

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
        /// <param name="agentUserId">Agent User Id for the agent.</param>
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        public async Task<List<MCPServerConfig>> ListToolServers(string agentUserId, string environmentId, string authToken)
        {
            return IsDevScenario() ? GetMCPServersFromManifest(environmentId) : await GetMCPServerFromToolingGatewayAsync(agentUserId, authToken);
        }

        /// <summary>
        /// Reads MCP server configurations from an endpoint for prod scenario.
        /// </summary>
        /// <returns>List of MCP server configurations</returns>
        private static async Task<List<MCPServerConfig>> GetMCPServerFromToolingGatewayAsync(string agentUserId, string authToken)
        {
            // TODO: Implement endpoint reading logic for production
            // This would typically involve making an HTTP call to a configuration service
            var mcpServers = new List<MCPServerConfig>();

            string configEndpoint = string.Empty;
            try
            {
                // Example implementation - replace with actual endpoint call
                using var httpClient = new HttpClient();
                configEndpoint = Utility.GetToolingGatewayForDigitalWorker(agentUserId);
                // Add any required headers for the config service
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await httpClient.GetStringAsync(configEndpoint);
                var configData = JsonSerializer.Deserialize<JsonElement>(response);

                if (configData.TryGetProperty("mcpServers", out var serversElement) &&
                    serversElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var serverElement in serversElement.EnumerateArray())
                    {
                        var serverConfig = ParseServerConfig(serverElement);
                        if (serverConfig != null)
                        {
                            mcpServers.Add(serverConfig);
                        }
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                throw new InvalidOperationException($"Failed to connect to MCP configuration endpoint '{configEndpoint}': {httpEx.Message}", httpEx);
            }
            catch (JsonException jsonEx)
            {
                throw new InvalidOperationException($"Failed to parse MCP server configuration response: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read MCP servers from endpoint: {ex.Message}", ex);
            }

            return mcpServers;
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

                if (serverElement.TryGetProperty("url", out var urlElement) && nameElement.ValueKind == JsonValueKind.String)
                {
                    endpoint = urlElement.GetString();
                }

                // Both Name and Endpoint are required
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(endpoint))
                {
                    return null;
                }

                return new MCPServerConfig
                {
                    mcpServerName = name,
                    url = endpoint
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

                if (serverElement.TryGetProperty("mcpServerName", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
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
                    url = fullUrl
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
