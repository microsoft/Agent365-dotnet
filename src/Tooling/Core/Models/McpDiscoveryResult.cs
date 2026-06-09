// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Internal result of MCP server discovery from the tooling gateway. Carries the parsed server
    /// list plus the response-level (aggregate) connection metadata used for connection gating.
    /// Sources that predate the connection fields (legacy bare-array gateway responses, dev-mode
    /// manifests) leave the aggregate fields null.
    /// </summary>
    internal sealed class McpDiscoveryResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpDiscoveryResult"/> class.
        /// </summary>
        /// <param name="servers">The parsed MCP server configurations.</param>
        /// <param name="allConnectionsUrl">Aggregate URL for all required connectors, or null.</param>
        /// <param name="missingConnectionsUrl">Aggregate URL for missing connectors, or null.</param>
        /// <param name="connectivityStatus">Aggregate connectivity status, or null when not supplied.</param>
        public McpDiscoveryResult(
            List<MCPServerConfig> servers,
            string? allConnectionsUrl = null,
            string? missingConnectionsUrl = null,
            string? connectivityStatus = null)
        {
            this.Servers = servers;
            this.AllConnectionsUrl = allConnectionsUrl;
            this.MissingConnectionsUrl = missingConnectionsUrl;
            this.ConnectivityStatus = connectivityStatus;
        }

        /// <summary>Gets the parsed MCP server configurations.</summary>
        public List<MCPServerConfig> Servers { get; }

        /// <summary>Gets the aggregate URL covering all connectors required across all servers, or null.</summary>
        public string? AllConnectionsUrl { get; }

        /// <summary>Gets the aggregate URL covering only the connectors still missing across all servers, or null.</summary>
        public string? MissingConnectionsUrl { get; }

        /// <summary>Gets the aggregate connectivity status across all servers, or null when not supplied.</summary>
        public string? ConnectivityStatus { get; }
    }
}
