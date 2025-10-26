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
        /// <param name="agentUserId">The unique identifier of the digital worker.</param>
        /// <returns>The tooling gateway URL for the digital worker.</returns>
        public static string GetToolingGatewayForDigitalWorker(string agentUserId)
        {
            // The endpoint needs to be updated based on the environment (prod, dev, etc.)
            return $"{GetMcpPlatformBaseUrl()}/agentGateway/agentApplicationInstances/{agentUserId}/mcpServers";
        }

        /// <summary>
        /// Gets the base URL for MCP servers based on the current environment.
        /// </summary>
        /// <returns>The base URL for MCP servers.</returns>
        public static string GetMcpBaseUrl()
        {
            var mcpPlatformBaseUrl = GetMcpPlatformBaseUrl();
            var environment = RuntimeUtility.GetCurrentEnvironment();
            if (environment.ToLowerInvariant() == "development")
            {
                var toolsMode = GetToolsMode();
                if (toolsMode == ToolsMode.MockMCPServer)
                {
                    return Environment.GetEnvironmentVariable("MOCK_MCP_SERVER_URL") ?? "http://localhost:5309/mcp-mock/agents/servers";
                }

                return Environment.GetEnvironmentVariable("MCP_DEVELOPMENT_BASE_URL")
                       ?? $"{mcpPlatformBaseUrl}/mcp/environments";
            }

            return $"{mcpPlatformBaseUrl}/mcp/environments";

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
        /// Constructs the full MCP server URL using the base URL, environment ID, and server name.
        /// </summary>
        /// <param name="environmentId">The environment ID.</param>
        /// <param name="serverName">The MCP server name.</param>
        /// <returns>The full MCP server URL.</returns>
        public static string BuildMcpServerUrl(string environmentId, string serverName)
        {
            var baseUrl = GetMcpBaseUrl();

            if (RuntimeUtility.GetCurrentEnvironment().ToLowerInvariant() == "development"
                && baseUrl.EndsWith("servers"))
            {
                return $"{baseUrl}/{serverName}";
            }
            else
            {
                return $"{baseUrl}/{environmentId}/servers/{serverName}";
            }
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