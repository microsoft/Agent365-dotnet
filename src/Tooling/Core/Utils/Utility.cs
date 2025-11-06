// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Utils
{
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
        /// <returns>The tooling gateway URL for the digital worker.</returns>
        public static string GetToolingGatewayForDigitalWorker(string agentInstanceId)
        {
            return $"{GetMcpPlatformBaseUrl()}/agents/{agentInstanceId}/mcpServers";
        }

        /// <summary>
        /// Gets the base URL for MCP servers.
        /// </summary>
        /// <returns>The base URL for MCP servers.</returns>
        public static string GetMcpBaseUrl()
        {
            var mcpPlatformBaseUrl = GetMcpPlatformBaseUrl();
            return $"{mcpPlatformBaseUrl}/agents/servers";
        }

        private static string GetMcpPlatformBaseUrl()
        {
            // First check for environment variable (takes precedence)
            var environmentVariableValue = Environment.GetEnvironmentVariable("MCP_PLATFORM_ENDPOINT");
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
        /// <returns>The full MCP server URL.</returns>
        public static string BuildMcpServerUrl(string serverName)
        {
            var baseUrl = GetMcpBaseUrl();
            return $"{baseUrl}/{serverName}";
        }

        /// <summary>
        /// Gets the configured tools mode from environment variable TOOLS_MODE.
        /// </summary>
        /// <returns>The configured tools mode, defaults to MCPPlatform if not set.</returns>
        public static ToolsMode GetToolsMode()
        {
            var toolsMode = Environment.GetEnvironmentVariable("TOOLS_MODE") ?? "MCPPlatform";
            return toolsMode.ToLowerInvariant() switch
            {
                "mockmcpserver" => ToolsMode.MockMCPServer,
                _ => ToolsMode.MCPPlatform
            };
        }
    }
}