// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Utils
{
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Extensions.Configuration;
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
            return $"{GetMcpPlatformBaseUrl(configuration)}/agents/v2/{agentInstanceId}/mcpServers";
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
        /// Resolves the OAuth scope to request when acquiring a token for the given MCP server.
        /// </summary>
        /// <remarks>
        /// Scope resolution priority:
        /// <list type="number">
        ///   <item>If <c>server.scope</c> is set, use it verbatim (explicit caller override).</item>
        ///   <item>If <c>server.audience</c> is a non-ATG GUID, construct a V2 per-audience scope:
        ///       <c>api://&lt;audience&gt;/.default</c>.</item>
        ///   <item>Otherwise (V1 — no audience, or audience equals ATG App ID), fall back to the
        ///       shared ATG scope from configuration, which respects the
        ///       <c>MCP_PLATFORM_AUTHENTICATION_SCOPE</c> environment override.</item>
        /// </list>
        /// </remarks>
        /// <param name="server">The MCP server configuration.</param>
        /// <param name="configuration">Application configuration used to resolve the V1 fallback scope.</param>
        /// <returns>The OAuth scope string to pass to <c>ExchangeTurnTokenAsync</c>.</returns>
        public static string ResolveTokenScopeForServer(MCPServerConfig server, IConfiguration configuration)
        {
            // V2: server carries its own audience different from the shared ATG App ID.
            // The gateway returns scope as a bare permission name (e.g. "Tools.ListInvoke.All"),
            // so we construct the full OAuth scope as "{audience}/{scope}" or "{audience}/.default".
            if (!string.IsNullOrWhiteSpace(server.audience) &&
                !string.Equals(server.audience, Constants.Authentication.AtgAppId, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(server.scope)
                    ? $"{server.audience}/{server.scope}"
                    : $"{server.audience}/.default";
            }

            // V1 fallback: use the ATG scope, honouring any environment override.
            // The scope field is ignored for V1 — callers pass a pre-acquired ATG token.
            return RuntimeUtility.GetMcpPlatformAuthenticationScope(configuration);
        }

        /// <summary>
        /// Determines whether the application is running in a local development scenario.
        /// </summary>
        /// <param name="configuration">Application configuration used to read environment name.</param>
        /// <returns><c>true</c> when the environment is <c>Development</c> (default when not set).</returns>
        internal static bool IsDevScenario(IConfiguration configuration)
        {
            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ??
                              configuration["DOTNET_ENVIRONMENT"] ??
                              "Development";
            return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
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