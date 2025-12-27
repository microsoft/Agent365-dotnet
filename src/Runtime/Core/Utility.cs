// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.Builder;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

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
        /// Creates a default HttpClient configured with standard timeout and user agent header.
        /// </summary>
        /// <param name="userAgentConfiguration">The implementation that contains User-Agent information. If null, uses default Agent 365 SDK configuration.</param>
        /// <param name="timeoutSeconds">Timeout in seconds. Defaults to 30 seconds.</param>
        /// <returns>A configured HttpClient instance.</returns>
        public static HttpClient GetDefaultHttpClient(IUserAgentConfiguration? userAgentConfiguration = null, int timeoutSeconds = 30)
        {
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentHelper.BuildUserAgent(userAgentConfiguration ?? Agent365SdkUserAgentConfiguration.Instance));
            return httpClient;
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
    }
}
