// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Utils
{
    using Microsoft.Extensions.Configuration;
    using System.Transactions;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;

    /// <summary>
    /// Provides utility methods for the Tooling components.
    /// </summary>
    public static class Utility
    {
        private const string McpPlatformProdBaseUrl = "https://agent365.svc.cloud.microsoft";

        /// <summary>
        /// Gets the tooling gateway URL for the specified digital worker.
        /// </summary>
        /// <param name="agentInstanceId">The unique identifier of the digital worker.</param>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The tooling gateway URL for the digital worker.</returns>
        public static string GetToolingGatewayForDigitalWorker(string agentInstanceId, IConfiguration configuration)
        {
            return $"{GetMcpPlatformBaseUrl(configuration)}/agents/{agentInstanceId}/mcpServers";
        }

        /// <summary>
        /// Gets the base URL for MCP servers.
        /// </summary>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The base URL for MCP servers.</returns>
        public static string GetMcpBaseUrl(IConfiguration configuration)
        {
            var mcpPlatformBaseUrl = GetMcpPlatformBaseUrl(configuration);
            return $"{mcpPlatformBaseUrl}/agents/servers";
        }

        private static string GetMcpPlatformBaseUrl(IConfiguration configuration)
        {
            // First check for configuration value (from any source, e.g., environment variable, appsettings.json, etc.)—takes precedence over default
            var environmentVariableValue = configuration["MCP_PLATFORM_ENDPOINT"];
            if (!string.IsNullOrEmpty(environmentVariableValue))
            {
                return environmentVariableValue;
            }

            // Default to production URL if no override is specified
            return McpPlatformProdBaseUrl;
        }

        /// <summary>
        /// Constructs the full MCP server URL using the base URL and server name.
        /// </summary>
        /// <param name="serverName">The MCP server name.</param>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The full MCP server URL.</returns>
        public static string BuildMcpServerUrl(string serverName, IConfiguration configuration)
        {
            var baseUrl = GetMcpBaseUrl(configuration);
            return $"{baseUrl}/{serverName}";
        }
    }
}