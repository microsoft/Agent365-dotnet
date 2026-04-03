// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Tooling.Models;

    /// <summary>
    /// Acquires OAuth tokens for MCP servers, routing V1 servers to the shared ATG token
    /// and V2 servers to per-audience tokens.
    /// </summary>
    internal interface IMcpTokenProvider
    {
        /// <summary>
        /// Returns a Bearer token for the specified MCP server.
        /// The scope is determined by <see cref="Utils.Utility.ResolveTokenScopeForServer"/>.
        /// </summary>
        /// <param name="server">The MCP server configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The raw token value (without the "Bearer " prefix).</returns>
        Task<string> GetTokenAsync(MCPServerConfig server, CancellationToken cancellationToken = default);
    }
}
