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
        /// Gets or sets the OAuth scope for this server.
        /// V2 servers supply their own scope (e.g. "api://&lt;audience&gt;/Tools.ListInvoke.All").
        /// Null for V1 servers that share the ATG token.
        /// </summary>
        public string? scope { get; set; }

        /// <summary>
        /// Gets or sets the OAuth audience (Application ID) for this server.
        /// Non-null for V2 servers that require a per-audience token.
        /// Null or equal to the ATG App ID for V1 servers.
        /// </summary>
        public string? audience { get; set; }

        /// <summary>
        /// Gets or sets the publisher identifier for this server.
        /// </summary>
        public string? publisher { get; set; }

        /// <summary>
        /// Gets or sets the URL containing the deduped union of all connectors required for this MCP
        /// server to operate. Populated by the discovery endpoint (<c>discoverMCPServers</c>); null
        /// when not supplied (for example, on the basic server-list path).
        /// </summary>
        public string? allConnectionsUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL containing the deduped union of only the connectors required for this
        /// MCP server that the connected user has not yet set up. Null when <c>connectivityStatus</c>
        /// is <c>Ready</c> (no setup needed) or when not supplied.
        /// </summary>
        public string? missingConnectionsUrl { get; set; }

        /// <summary>
        /// Gets or sets the connectivity status for this MCP server (for example, <c>Ready</c> when all
        /// required connectors are already connected). Compare case-insensitively. Null when not supplied.
        /// </summary>
        public string? connectivityStatus { get; set; }

        /// <summary>
        /// Gets or sets per-server HTTP headers, including the Authorization header populated
        /// by <c>AttachPerAudienceTokensAsync</c> before tool connections are established.
        /// Null until token attachment has run; callers should treat a missing Authorization
        /// header as a signal to fall back to the shared ATG token.
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }
    }
}
