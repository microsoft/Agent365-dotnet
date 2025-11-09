// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

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
        /// The MCP platform authentication scope from environment variable MCP_PLATFORM_AUTHENTICATION_SCOPE,
        /// or the default production scope if not set.
        /// </returns>
        public static string GetMcpPlatformAuthenticationScope(IConfiguration configuration)
        {
            return configuration["MCP_PLATFORM_AUTHENTICATION_SCOPE"] ??
                   McpPlatformProdAuthenticationScope;
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
