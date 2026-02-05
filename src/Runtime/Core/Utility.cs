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
        /// <para><b>WARNING: NO SIGNATURE VERIFICATION</b> - This method uses JwtSecurityTokenHandler.ReadJwtToken()
        /// which does NOT verify the token signature. The token claims can be spoofed by malicious actors.</para>
        /// <para>This method is ONLY suitable for logging, analytics, and diagnostics purposes.
        /// Do NOT use the returned value for authorization, access control, or security decisions.</para>
        /// <para>Decodes the current token and retrieves the App ID (appid or azp claim).</para>
        /// <para>Note: Returns a default GUID ('00000000-0000-0000-0000-000000000000') for empty tokens
        /// for backward compatibility with callers that expect a valid-looking GUID.
        /// For agent identification where empty string is preferred, use <see cref="GetAgentIdFromToken"/>.</para>
        /// </summary>
        /// <param name="token">Token to Decode</param>
        /// <returns>AppId, or default GUID for empty token</returns>
        /// <exception cref="ArgumentException">Thrown when token format is invalid</exception>
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
        /// <para><b>WARNING: NO SIGNATURE VERIFICATION</b> - This method uses JwtSecurityTokenHandler.ReadJwtToken()
        /// which does NOT verify the token signature. The token claims can be spoofed by malicious actors.</para>
        /// <para>This method is ONLY suitable for logging, analytics, and diagnostics purposes.
        /// Do NOT use the returned value for authorization, access control, or security decisions.</para>
        /// <para>Decodes the token and retrieves the best available agent identifier.
        /// Checks claims in priority order: xms_par_app_azp (agent blueprint ID) > appid > azp.</para>
        /// <para>Note: Returns empty string for empty/missing tokens (unlike <see cref="GetAppIdFromToken"/> which
        /// returns a default GUID). This allows callers to omit headers when no identifier is available.</para>
        /// </summary>
        /// <param name="token">JWT token to decode</param>
        /// <returns>Agent ID (GUID) or empty string if not found or token is empty</returns>
        public static string GetAgentIdFromToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return string.Empty;
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Priority: xms_par_app_azp (agent blueprint ID) > appid > azp
                var blueprintClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "xms_par_app_azp");
                if (!string.IsNullOrEmpty(blueprintClaim?.Value))
                {
                    return blueprintClaim.Value;
                }

                var appIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "appid");
                if (!string.IsNullOrEmpty(appIdClaim?.Value))
                {
                    return appIdClaim.Value;
                }

                var azpClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "azp");
                return azpClaim?.Value ?? string.Empty;
            }
            catch
            {
                // Silent error handling - return empty string on decode failure
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the application name from the entry assembly.
        /// </summary>
        /// <returns>Application name or null if not available.</returns>
        public static string? GetApplicationName()
        {
            return System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name;
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
