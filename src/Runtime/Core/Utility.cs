// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
        /// <param name="httpClientFactory">The IHttpClientFactory to create the HttpClient instance. This parameter is required.</param>
        /// <param name="userAgentConfiguration">The implementation that contains User-Agent information. If null, uses default Agent 365 SDK configuration.</param>
        /// <param name="timeoutSeconds">Timeout in seconds. Defaults to 30 seconds.</param>
        /// <returns>A configured HttpClient instance.</returns>
        public static HttpClient GetDefaultHttpClient(IHttpClientFactory httpClientFactory, IUserAgentConfiguration? userAgentConfiguration = null, int timeoutSeconds = 30)
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgentHelper.BuildUserAgent(userAgentConfiguration ?? Agent365SdkUserAgentConfiguration.Instance));
            return httpClient;
        }

        /// <summary>
        /// Decodes the token and retrieves the user's UPN (email) from the "upn" or "preferred_username" claim.
        /// In Teams, <c>Activity.From.Name</c> is the display name, not the email.
        /// This method provides the correct email/UPN for user identity in registration URLs.
        /// </summary>
        /// <param name="token">JWT token to decode.</param>
        /// <returns>The user's email/UPN, or null if not found or the token is empty.</returns>
        public static string? GetUpnFromToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var upnClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "upn" || c.Type == "preferred_username");
                return upnClaim?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Decodes the token and retrieves the user's Object ID (oid) from the "oid" or
        /// "http://schemas.microsoft.com/identity/claims/objectidentifier" claim.
        /// The object ID is a stable, tenant-scoped GUID that uniquely identifies the user.
        /// </summary>
        /// <param name="token">JWT token to decode.</param>
        /// <returns>The user's object ID, or null if not found or the token is empty.</returns>
        public static string? GetObjectIdFromToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                var oidClaim = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == "oid" || c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
                return oidClaim?.Value;
            }
            catch
            {
                return null;
            }
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
