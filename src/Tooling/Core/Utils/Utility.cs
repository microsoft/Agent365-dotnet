// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

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

        /// <summary>
        /// Gets the URL of the chat history endpoint used by the real-time threat protection service.
        /// </summary>
        /// <param name="configuration">Configuration Collection used to resolve the MCP platform base URL.</param>
        /// <returns>
        /// An absolute URL that tooling components can use to send or retrieve chat messages for
        /// real-time threat protection scenarios.
        /// </returns>
        /// <remarks>
        /// Call this method when constructing HTTP requests that need to access the chat-message history
        /// for real-time threat protection. The returned URL already includes the MCP platform base address
        /// and the fixed path segment <c>/agents/real-time-threat-protection/chat-message</c>.
        /// </remarks>
        public static string GetChatHistoryEndpoint(IConfiguration configuration)
        {
            return $"{GetMcpPlatformBaseUrl(configuration)}/agents/real-time-threat-protection/chat-message";
        }
    }
}