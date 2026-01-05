// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Runtime
{
    /// <summary>
    /// Defines the configuration for building a User-Agent header string.
    /// </summary>
    public interface IUserAgentConfiguration
    {
        /// <summary>
        /// Gets the product name to include in the User-Agent header.
        /// </summary>
        string ProductName { get; }

        /// <summary>
        /// Gets the version of the product.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the optional orchestrator name to include in the User-Agent header.
        /// </summary>
        string? OrchestratorName { get; }
    }
}
