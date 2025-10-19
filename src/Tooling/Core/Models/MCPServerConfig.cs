// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Models
{
    /// <summary>
    /// Represents the configuration for an MCP server, including its name and endpoint.
    /// </summary>
    public class MCPServerConfig
    {
        /// <summary>
        /// Gets or sets the name of the MCP server.
        /// </summary>
        public required string mcpServerName { get; set; }

        /// <summary>
        /// Gets or sets the url of the MCP server.
        /// </summary>
        public required string url { get; set; }
    }
}
