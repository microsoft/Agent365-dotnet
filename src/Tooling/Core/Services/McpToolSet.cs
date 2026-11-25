// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Agents.A365.Tooling.Models;
    using ModelContextProtocol.Client;

    /// <summary>
    /// The MCP servers and tools found by one enumeration, together with the MCP clients that serve the tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each <see cref="McpClientTool"/> calls its MCP server through the client that listed it, and that client sends the
    /// request headers, including the <c>Authorization</c> token, of the call that created it. Use a tool set for one turn
    /// and dispose it when the turn ends, for example with <c>await using</c>. Disposing the tool set disposes its MCP clients,
    /// ends their MCP sessions, closes their connections and disposes their <see cref="System.Net.Http.HttpClient"/> instances.
    /// Tools from a disposed tool set can't be invoked.
    /// </para>
    /// <para>
    /// The tool set enumerates the tools of all servers, server by server. It is also enumerable as
    /// <see cref="Microsoft.Extensions.AI.AITool"/> instances, so it can be added directly to an agent's tool list.
    /// </para>
    /// </remarks>
    public sealed class McpToolSet : IReadOnlyList<McpClientTool>, IAsyncDisposable
    {
        private readonly IReadOnlyList<McpClientTool> _tools;
        private readonly IAsyncDisposable[] _ownedResources;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolSet"/> class.
        /// </summary>
        /// <param name="servers">The MCP servers that were found.</param>
        /// <param name="toolsByServer">The tools of each server that responded, keyed by MCP server name.</param>
        /// <param name="ownedResources">
        /// The resources that serve the tools, such as their MCP clients, that the tool set disposes when it is disposed.
        /// When <see langword="null"/>, disposing the tool set doesn't dispose anything.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="servers"/> or <paramref name="toolsByServer"/> is <see langword="null"/>.</exception>
        public McpToolSet(
            IEnumerable<MCPServerConfig> servers,
            IReadOnlyDictionary<string, IReadOnlyList<McpClientTool>> toolsByServer,
            IEnumerable<IAsyncDisposable>? ownedResources = null)
        {
            ArgumentNullException.ThrowIfNull(servers);
            ArgumentNullException.ThrowIfNull(toolsByServer);

            Servers = servers.ToList();
            ToolsByServer = toolsByServer;
            _tools = toolsByServer.Values.SelectMany(tools => tools).ToList();
            _ownedResources = ownedResources?.Where(resource => resource is not null).ToArray() ?? Array.Empty<IAsyncDisposable>();
        }

        /// <summary>
        /// Gets the MCP servers that were found, including servers whose tools couldn't be listed.
        /// </summary>
        public IReadOnlyList<MCPServerConfig> Servers { get; }

        /// <summary>
        /// Gets the tools of each server that responded, keyed by MCP server name.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<McpClientTool>> ToolsByServer { get; }

        /// <summary>
        /// Gets the number of tools from all servers.
        /// </summary>
        public int Count => _tools.Count;

        /// <summary>
        /// Gets the tool at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the tool.</param>
        /// <returns>The tool at <paramref name="index"/>.</returns>
        public McpClientTool this[int index] => _tools[index];

        /// <summary>
        /// Returns an enumerator over the tools of all servers.
        /// </summary>
        /// <returns>An enumerator over the tools of all servers.</returns>
        public IEnumerator<McpClientTool> GetEnumerator() => _tools.GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Disposes the MCP clients that serve the tools. Calling it more than once has no further effect.
        /// </summary>
        /// <returns>A task that completes when every client is disposed.</returns>
        /// <remarks>
        /// Clients are disposed in parallel. An error from disposing one client is ignored so that the other clients
        /// are still disposed, and this method doesn't throw.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            await Task.WhenAll(_ownedResources.Select(DisposeQuietlyAsync)).ConfigureAwait(false);
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable resource)
        {
            try
            {
                await resource.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Keep disposing the other clients.
            }
        }
    }
}
