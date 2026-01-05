// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Models
{
    /// <summary>
    /// Represents the configuration for an MCP server, including its name, endpoint, and additional metadata.
    /// </summary>
    public class MCPServerConfig
    {
        /// <summary>
        /// Gets or sets the name of the MCP server.
        /// </summary>
        public required string mcpServerName { get; set; }

        /// <summary>
        /// Gets or sets the id of the MCP server.
        /// </summary>
        public required string id { get; set; }

        /// <summary>
        /// Gets or sets the url of the MCP server.
        /// </summary>
        public required string url { get; set; }

        /// <summary>
        /// Gets or sets the scope of the MCP server.
        /// </summary>
        public required string scope { get; set; }

        /// <summary>
        /// Gets or sets the audience of the MCP server.
        /// </summary>
        public required string audience { get; set; }

        /// <summary>
        /// Gets or sets the publisher of the MCP server.
        /// </summary>
        public required string publisher { get; set; }
    }
}
