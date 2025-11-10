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
        /// Gets the base URL for MCP servers based on the current environment.
        /// </summary>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The base URL for MCP servers.</returns>
        public static string GetMcpBaseUrl(IConfiguration configuration)
        {
            var mcpPlatformBaseUrl = GetMcpPlatformBaseUrl(configuration);
            if (!UseEnvironmentId(configuration))
            {
                return $"{mcpPlatformBaseUrl}/agents/servers";
            }


            return $"{mcpPlatformBaseUrl}/mcp/environments";

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
        /// Constructs the full MCP server URL using the base URL, environment ID, and server name.
        /// </summary>
        /// <param name="environmentId">The environment ID.</param>
        /// <param name="serverName">The MCP server name.</param>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The full MCP server URL.</returns>
        public static string BuildMcpServerUrl(string environmentId, string serverName, IConfiguration configuration)
        {
            var baseUrl = GetMcpBaseUrl(configuration);

            if (!UseEnvironmentId(configuration) || ((RuntimeUtility.GetCurrentEnvironment(configuration).ToLowerInvariant() == "development"
                && baseUrl.EndsWith("servers"))))
            {
                return $"{baseUrl}/{serverName}";
            }
            else
            {
                return $"{baseUrl}/{environmentId}/servers/{serverName}";
            }
        }

        /// <summary>
        /// Gets the configured tools mode from the configuration (e.g., environment variable TOOLS_MODE, appsettings.json, etc.).
        /// </summary>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>The configured tools mode, defaults to MCPPlatform if not set.</returns>
        public static ToolsMode GetToolsMode(IConfiguration configuration)
        {
            var toolsMode = configuration["TOOLS_MODE"] ?? "MCPPlatform";
            return toolsMode.ToLowerInvariant() switch
            {
                "mockmcpserver" => ToolsMode.MockMCPServer,
                _ => ToolsMode.MCPPlatform
            };
        }
        /// <summary>
        /// Determines whether to use environment ID based on the USE_ENVIRONMENT_ID value from the provided configuration.
        /// </summary>
        /// <param name="configuration">Configuration Collection</param>
        /// <returns>True if environment ID should be used; otherwise, false.</returns>
        public static bool UseEnvironmentId(IConfiguration configuration)
        {
            bool useEnvironmentId = true;
            if (bool.TryParse(configuration["USE_ENVIRONMENT_ID"], out bool tempUseEnvironmentId))
                useEnvironmentId = tempUseEnvironmentId;

            if (useEnvironmentId && string.IsNullOrEmpty(configuration["ENVIRONMENT_ID"]))
            {
                // if Use EnvironmentId is true, but ENVIRONMENT_ID is not set, default to false
                System.Diagnostics.Trace.TraceWarning("USE_ENVIRONMENT_ID is set to true, but ENVIRONMENT_ID is not set. Defaulting USE_ENVIRONMENT_ID to false.");
                return false; 
            }
            return useEnvironmentId;
        }
    }
}