// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Models
{
    using Microsoft.Agents.A365.Runtime;

    /// <summary>
    /// Tooling options for listing tool servers.
    /// </summary>
    public class ToolOptions
    {
        /// <summary>
        /// Gets or sets the user agent configuration for this orchestrator.
        /// </summary>
        public IUserAgentConfiguration? UserAgentConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for MCP client initialization.
        /// This includes the time for the MCP protocol handshake (initialize/initialized exchange)
        /// and the underlying HTTP connection. Increase this value if the MCP server performs
        /// slow operations during initialization (e.g., token exchanges in test environments).
        /// When null, the MCP SDK default timeout is used.
        /// </summary>
        public int? McpClientInitializationTimeoutSeconds { get; set; }
    }
}
