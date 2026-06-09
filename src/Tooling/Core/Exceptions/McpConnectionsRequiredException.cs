// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Thrown when one or more configured MCP servers are not yet connection-ready.
    /// The tooling gateway reports an aggregate connectivity status other than <c>Ready</c>
    /// (for example, <c>Pending</c>) when the agent's MCP servers have downstream connections that the
    /// user has not yet established. Callers should surface <see cref="MissingConnectionsUrl"/> to the
    /// user to complete setup, then retry on a later turn once the connections are in place.
    /// </summary>
    public class McpConnectionsRequiredException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="McpConnectionsRequiredException"/> class.
        /// </summary>
        /// <param name="missingConnectionsUrl">URL the user can visit to set up the missing connections, when provided by the gateway.</param>
        /// <param name="connectivityStatus">The aggregate connectivity status reported by the gateway (for example, <c>Pending</c>).</param>
        /// <param name="serverNames">The names of the MCP servers that are not yet connection-ready.</param>
        public McpConnectionsRequiredException(
            string? missingConnectionsUrl,
            string? connectivityStatus,
            IReadOnlyList<string> serverNames)
            : base(BuildMessage(missingConnectionsUrl, connectivityStatus, serverNames))
        {
            this.MissingConnectionsUrl = missingConnectionsUrl;
            this.ConnectivityStatus = connectivityStatus;
            this.ServerNames = serverNames;
        }

        /// <summary>Gets the URL the user can visit to set up the missing connections, or null when not provided.</summary>
        public string? MissingConnectionsUrl { get; }

        /// <summary>Gets the aggregate connectivity status reported by the gateway, or null when not provided.</summary>
        public string? ConnectivityStatus { get; }

        /// <summary>Gets the names of the MCP servers that are not yet connection-ready.</summary>
        public IReadOnlyList<string> ServerNames { get; }

        private static string BuildMessage(string? missingConnectionsUrl, string? connectivityStatus, IReadOnlyList<string> serverNames)
        {
            string serversText = serverNames is { Count: > 0 } ? string.Join(", ", serverNames) : "(unknown)";
            return $"MCP servers [{serversText}] require connection setup (connectivityStatus={connectivityStatus}). " +
                   $"Set up missing connections at: {missingConnectionsUrl}";
        }
    }
}
