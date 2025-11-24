// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Settings
{
    /// <summary>
    /// Contains constants for the Agent Settings module.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The default base URL for the Agent 365 platform.
        /// </summary>
        public const string DefaultPlatformBaseUrl = "https://agent365.svc.cloud.microsoft";

        /// <summary>
        /// The configuration key for the platform endpoint override.
        /// </summary>
        public const string PlatformEndpointConfigKey = "MCP_PLATFORM_ENDPOINT";

        /// <summary>
        /// The configuration key for the platform authentication scope.
        /// </summary>
        public const string PlatformAuthScopeConfigKey = "MCP_PLATFORM_AUTHENTICATION_SCOPE";

        /// <summary>
        /// The default authentication scope for the platform.
        /// </summary>
        public const string DefaultPlatformAuthScope = "ea9ffc3e-8a23-4a7d-836d-234d7c7565c1/.default";
    }
}
