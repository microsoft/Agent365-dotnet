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
        /// <para>
        /// A server is classified as <b>V1</b> (shared ATG token) when <c>server.audience</c> is
        /// absent, blank, or equal to the ATG App ID in either its bare-GUID form
        /// (<c>ea9ffc3e-8a23-4a7d-836d-234d7c7565c1</c>) or its <c>api://&lt;AtgAppId&gt;</c>
        /// URI form.  For V1 servers the method returns the ATG scope from configuration,
        /// honouring the <c>MCP_PLATFORM_AUTHENTICATION_SCOPE</c> environment override.
        /// <c>server.scope</c> is ignored for V1 servers.
        /// </para>
        /// <para>
        /// A server is classified as <b>V2</b> (per-audience token) when <c>server.audience</c>
        /// is present and identifies an application other than ATG.  The scope is built verbatim
        /// from the audience string exactly as returned by the gateway — no <c>api://</c>
        /// prefix is added or removed:
        /// <list type="bullet">
        ///   <item>bare-GUID audience with <c>server.scope</c> set →
        ///       <c>&lt;guid&gt;/&lt;scope&gt;</c></item>
        ///   <item>bare-GUID audience without <c>server.scope</c> →
        ///       <c>&lt;guid&gt;/.default</c></item>
        ///   <item><c>api://&lt;guid&gt;</c> audience with <c>server.scope</c> set →
        ///       <c>api://&lt;guid&gt;/&lt;scope&gt;</c></item>
        ///   <item><c>api://&lt;guid&gt;</c> audience without <c>server.scope</c> →
        ///       <c>api://&lt;guid&gt;/.default</c></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="server">The MCP server configuration.</param>
        /// <param name="configuration">Application configuration used to resolve the V1 fallback scope.</param>
        /// <returns>The OAuth scope string to pass to <c>ExchangeTurnTokenAsync</c>.</returns>
        public static string ResolveTokenScopeForServer(MCPServerConfig server, IConfiguration configuration)
        {
            // V2: server carries its own audience different from the shared ATG App ID.
            // IsAtgAudience accepts both "ea9ffc3e-..." and "api://ea9ffc3e-..." so that a gateway
            // returning either form is correctly identified as V1, not silently routed to V2.
            if (!string.IsNullOrWhiteSpace(server.audience) && !IsAtgAudience(server.audience))
            {
                // Build the scope from the audience exactly as the gateway returned it,
                // so "guid" and "api://guid" each produce a scope consistent with their own form.
                return !string.IsNullOrWhiteSpace(server.scope)
                    ? $"{server.audience}/{server.scope}"
                    : $"{server.audience}/.default";
            }

            // V1 fallback: use the ATG scope, honouring any environment override.
            // The scope field is ignored for V1 — callers pass a pre-acquired ATG token.
            return RuntimeUtility.GetMcpPlatformAuthenticationScope(configuration);
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="audience"/> identifies the shared ATG application,
        /// accepting both the bare GUID form (<c>{AtgAppId}</c>) and the equivalent
        /// <c>api://{AtgAppId}</c> URI form that the tooling gateway may return.
        /// </summary>
        /// <param name="audience">The audience value from an MCP server configuration.</param>
        internal static bool IsAtgAudience(string audience) =>
            string.Equals(audience, Constants.Authentication.AtgAppId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(audience, $"api://{Constants.Authentication.AtgAppId}", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the dev-mode Bearer token for the given MCP server name.
        /// Resolution order:
        /// <list type="number">
        ///   <item>Per-server variable: <c>BEARER_TOKEN_{SERVERNAME_UPPER}</c> (hyphens replaced with underscores).</item>
        ///   <item>Shared fallback variable: <c>BEARER_TOKEN</c>.</item>
        /// </list>
        /// Returns <c>null</c> when neither variable is set.
        /// </summary>
        /// <param name="serverName">The MCP server name used to derive the per-server env-var key.</param>
        /// <param name="configuration">Application configuration to read env vars from.</param>
        internal static string? GetDevBearerToken(string serverName, IConfiguration configuration)
        {
            var normalizedName = serverName.ToUpperInvariant().Replace('-', '_');
            var perServerValue = configuration[$"BEARER_TOKEN_{normalizedName}"];
            return !string.IsNullOrWhiteSpace(perServerValue) ? perServerValue : configuration["BEARER_TOKEN"];
        }

        /// <summary>
        /// Determines whether the application is running in a local development scenario.
        /// </summary>
        /// <param name="configuration">Application configuration used to read environment name.</param>
        /// <returns>
        /// <c>true</c> when <c>ASPNETCORE_ENVIRONMENT</c> or <c>DOTNET_ENVIRONMENT</c> is set to
        /// <c>Development</c>; <c>false</c> when neither variable is set or either holds a different value.
        /// </returns>
        internal static bool IsDevScenario(IConfiguration configuration)
        {
            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ??
                              configuration["DOTNET_ENVIRONMENT"];
            return environment != null && environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
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