// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.Builder;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Partial class containing tool enumeration functionality.
    /// </summary>
    public partial class McpToolServerConfigurationService
    {
        /// <inheritdoc/>
        public virtual async Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(
            string agentInstanceId,
            string authToken,
            IMcpTokenProvider tokenProvider,
            ITurnContext turnContext,
            ToolOptions toolOptions)
        {
            var toolsByServer = new Dictionary<string, IList<McpClientTool>>(StringComparer.OrdinalIgnoreCase);

            List<MCPServerConfig> servers;
            try
            {
                servers = await ListToolServersWithTokensAsync(
                    agentInstanceId,
                    authToken,
                    tokenProvider,
                    toolOptions).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is HttpRequestException ||
                ex is TaskCanceledException ||
                ex is TimeoutException)
            {
                _logger.LogError(ex, "Failed to list MCP tool servers for AgentInstanceId={AgentInstanceId}", agentInstanceId);
                return (new List<MCPServerConfig>(), toolsByServer);
            }

            if (servers.Count == 0)
            {
                _logger.LogInformation("No MCP servers configured for agentInstanceId={AgentInstanceId}", agentInstanceId);
                return (servers, toolsByServer);
            }

            var validServers = servers.Where(server =>
            {
                if (string.IsNullOrWhiteSpace(server.mcpServerName) || string.IsNullOrWhiteSpace(server.url))
                {
                    _logger.LogWarning(
                        "Skipping invalid MCP server config: Name='{Name}', Url='{Url}'",
                        server.mcpServerName,
                        server.url);
                    return false;
                }
                return true;
            }).ToList();

            var tasks = validServers.Select(async server =>
            {
                try
                {
                    // Per-server token is already in server.Headers; GetMcpClientToolsAsync
                    // extracts it via ResolveEffectiveToken, falling back to authToken for V1.
                    var mcpTools = await GetMcpClientToolsAsync(
                        turnContext,
                        server,
                        authToken,
                        toolOptions).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Successfully loaded {ToolCount} tools from MCP server '{ServerName}'",
                        mcpTools.Count,
                        server.mcpServerName);

                    return (ServerName: server.mcpServerName, Tools: mcpTools, Success: true);
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException ||
                    ex is System.Net.Http.HttpRequestException ||
                    ex is TaskCanceledException ||
                    ex is TimeoutException)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load tools from MCP server '{ServerName}' at '{Url}': {Error}",
                        server.mcpServerName,
                        server.url,
                        ex.Message);

                    return (ServerName: server.mcpServerName, Tools: (IList<McpClientTool>)Array.Empty<McpClientTool>(), Success: false);
                }
            });

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var result in results.Where(r => r.Success))
            {
                toolsByServer[result.ServerName] = result.Tools;
            }

            _logger.LogInformation(
                "MCP server discovery completed: Total servers={ServerCount}, Servers with tools={SuccessCount}",
                servers.Count,
                toolsByServer.Count);

            return (servers, toolsByServer);
        }

        /// <inheritdoc/>
        public virtual async Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(
            string agentInstanceId,
            string authToken,
            ITurnContext turnContext,
            ToolOptions toolOptions)
        {
            var toolsByServer = new Dictionary<string, IList<McpClientTool>>(StringComparer.OrdinalIgnoreCase);

            List<MCPServerConfig> servers;
            try
            {
                servers = await ListToolServersAsync(
                    agentInstanceId,
                    authToken,
                    toolOptions).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list MCP tool servers for AgentInstanceId={AgentInstanceId}", agentInstanceId);
                return (new List<MCPServerConfig>(), toolsByServer);
            }

            if (servers.Count == 0)
            {
                _logger.LogInformation("No MCP servers configured for agentInstanceId={AgentInstanceId}", agentInstanceId);
                return (servers, toolsByServer);
            }

            // Guard: this overload cannot perform per-audience OBO exchange because it has no
            // token provider.  In production, V2 servers (distinct audience GUID) would silently
            // receive the wrong shared ATG token, causing 401s at the server.  Fail fast instead
            // of silently sending the wrong credential.  Dev mode is exempt because the manifest
            // normally contains only V1 servers and tokens come from environment variables.
            if (!IsDevScenario())
            {
                var v2ServerNames = servers
                    .Where(s => !string.IsNullOrWhiteSpace(s.audience) && !Utility.IsAtgAudience(s.audience))
                    .Select(s => s.mcpServerName)
                    .ToList();

                if (v2ServerNames.Count > 0)
                {
                    var names = string.Join(", ", v2ServerNames.Select(n => $"'{n}'"));
                    throw new InvalidOperationException(
                        $"MCP server(s) {names} require per-audience tokens (V2) that cannot be " +
                        $"acquired without a token provider. Migrate to the overload that accepts " +
                        $"IMcpTokenProvider, or use AddToolServersToAgent / GetMcpToolsAsync in the " +
                        $"extension service, which handle per-audience token acquisition automatically.");
                }
            }

            // Filter valid servers first
            var validServers = servers.Where(server =>
            {
                if (string.IsNullOrWhiteSpace(server.mcpServerName) || string.IsNullOrWhiteSpace(server.url))
                {
                    _logger.LogWarning(
                        "Skipping invalid MCP server config: Name='{Name}', Url='{Url}'",
                        server.mcpServerName,
                        server.url);
                    return false;
                }
                return true;
            }).ToList();

            // Enumerate tools from all servers in parallel
            var tasks = validServers.Select(async server =>
            {
                try
                {
                    var mcpTools = await GetMcpClientToolsAsync(
                        turnContext,
                        server,
                        authToken,
                        toolOptions).ConfigureAwait(false);

                    _logger.LogInformation(
                        "Successfully loaded {ToolCount} tools from MCP server '{ServerName}'",
                        mcpTools.Count,
                        server.mcpServerName);

                    return (ServerName: server.mcpServerName, Tools: mcpTools, Success: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load tools from MCP server '{ServerName}' at '{Url}': {Error}",
                        server.mcpServerName,
                        server.url,
                        ex.Message);

                    return (ServerName: server.mcpServerName, Tools: (IList<McpClientTool>)Array.Empty<McpClientTool>(), Success: false);
                }
            });

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Populate the dictionary with successful results
            foreach (var result in results.Where(r => r.Success))
            {
                toolsByServer[result.ServerName] = result.Tools;
            }

            _logger.LogInformation(
                "MCP server discovery completed: Total servers={ServerCount}, Servers with tools={SuccessCount}",
                servers.Count,
                toolsByServer.Count);

            return (servers, toolsByServer);
        }

        /// <inheritdoc/>
        public virtual async Task<IList<McpClientTool>> EnumerateAllToolsAsync(
            string agentInstanceId,
            string authToken,
            ITurnContext turnContext,
            ToolOptions toolOptions)
        {
            var (_, toolsByServer) = await EnumerateToolsFromServersAsync(
                agentInstanceId,
                authToken,
                turnContext,
                toolOptions).ConfigureAwait(false);

            var allTools = new List<McpClientTool>();
            foreach (var tools in toolsByServer.Values)
            {
                allTools.AddRange(tools);
            }

            _logger.LogInformation(
                "Enumerated {TotalToolCount} total MCP tools for agent {AgentInstanceId}",
                allTools.Count,
                agentInstanceId);

            return allTools;
        }
    }
}
