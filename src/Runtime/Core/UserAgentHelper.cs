// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Microsoft.Agents.A365.Runtime
{
    /// <summary>
    /// Provides utilities for building User-Agent header strings.
    /// </summary>
    public static class UserAgentHelper
    {
        /// <summary>
        /// Builds a User-Agent header string from the provided configuration.
        /// </summary>
        /// <param name="config">The user agent configuration.</param>
        /// <returns>A formatted User-Agent header string.</returns>
        public static string BuildUserAgent(IUserAgentConfiguration config)
        {
            var osDescription = RuntimeInformation.OSDescription;
            var frameworkDescription = RuntimeInformation.FrameworkDescription;
            
            var orchestratorPart = string.IsNullOrEmpty(config.OrchestratorName) 
                ? "" 
                : $"; {config.OrchestratorName}";

            return $"{config.ProductName}/{config.Version} ({osDescription}; {frameworkDescription}{orchestratorPart})";
        }
    }
}
