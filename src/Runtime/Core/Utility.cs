// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.Agents.A365.Runtime.Utils
{
    /// <summary>
    /// Provides utility methods for the Microsoft.Agents.A365.Runtime.Common namespace.
    /// </summary>
    public static class Utility
    {
        private const string McpPlatformProdAuthenticationScope = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default";

        /// <summary>
        /// Gets the MCP platform authentication scope.
        /// </summary>
        /// <returns>
        /// The MCP platform authentication scope from configuration (e.g., environment variable MCP_PLATFORM_AUTHENTICATION_SCOPE,
        /// appsettings.json, or other configuration sources), or the default production scope if not set.
        /// </returns>
        public static string GetMcpPlatformAuthenticationScope(IConfiguration configuration)
        {
            return configuration["MCP_PLATFORM_AUTHENTICATION_SCOPE"] ??
                   McpPlatformProdAuthenticationScope;
        }

        /// <summary>
        /// Gets the current environment name.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <returns>The current environment name.</returns>
        public static string GetCurrentEnvironment(IConfiguration configuration)
        {
            return configuration["ASPNETCORE_ENVIRONMENT"] ??
                   configuration["DOTNET_ENVIRONMENT"] ??
                   "Development";
        }

        /// <summary>
        /// Decodes the current token and retrieves the App ID (appid or azp claim).
        /// </summary>
        /// <param name="token">Token to Decode</param>
        /// <returns>AppId</returns>
        public static string GetAppIdFromToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Guid.Empty.ToString();
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var appIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "appid" || c.Type == "azp");
            return appIdClaim?.Value ?? string.Empty;
        }

        /// <summary>
        /// Resolves the agent identity from the turn context or auth token.
        /// </summary>
        /// <param name="context">Turn Context of the turn.</param>
        /// <param name="authToken">Auth token if available.</param>
        /// <returns></returns>
        public static string ResolveAgentIdentity(ITurnContext context, string authToken)
        {
            // App ID is required to pass to MCP server URL.
            string agenticAppId = context.Activity.IsAgenticRequest()
                ? context.Activity.GetAgenticInstanceId()
                : Runtime.Utils.Utility.GetAppIdFromToken(authToken);
            return agenticAppId;
        }

        /// <summary>
        /// Gets the User-Agent header string.
        /// </summary>
        /// <param name="orchestrator">The orchestrator name to include in the User-Agent string.</param>
        /// <returns>The User-Agent header string.</returns>
        public static string GetUserAgentHeader(string orchestrator = "")
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown";
            var frameworkDescription = RuntimeInformation.FrameworkDescription;
            var osType  = RuntimeInformation.OSDescription;
            var orchestratorString = string.IsNullOrEmpty(orchestrator) ? "" : $"; {orchestrator}";
            return $"Agent365SDK/{version} ({osType}; {frameworkDescription}{orchestratorString})";
        }
    }
}
