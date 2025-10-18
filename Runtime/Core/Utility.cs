// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Runtime.Utils
{
    /// <summary>
    /// Provides utility methods for the Microsoft.Kairo.Sdk.Runtime.Common namespace.
    /// </summary>
    public static class Utility
    {
        private const string McpPlatformTestAuthenticationScope = "https://api.test.powerplatform.com/.default";
        private const string McpPlatformProdAuthenticationScope = "https://api.powerplatform.com/.default";

        /// <summary>
        /// Gets the MCP platform authentication scope based on the current environment.
        /// </summary>
        /// <returns>
        /// The MCP platform authentication scope.
        /// </returns>
        public static string GetMcpPlatformAuthenticationScope()
        {
            var environment = GetCurrentEnvironment();
            return environment.ToLowerInvariant() switch
            {
                "development" => McpPlatformTestAuthenticationScope,
                "test" => McpPlatformTestAuthenticationScope,
                "production" => McpPlatformProdAuthenticationScope,
                _ => McpPlatformProdAuthenticationScope
            };
        }

        /// <summary>
        /// Gets the current environment name.
        /// </summary>
        /// <returns>The current environment name.</returns>
        public static string GetCurrentEnvironment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                   Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                   "Development";
        }
    }
}
