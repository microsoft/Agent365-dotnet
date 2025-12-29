// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Services
{
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Client;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides common functionality for enumerating MCP tools from configured servers.
    /// This service encapsulates the shared logic used across different agent framework implementations.
    /// </summary>
    public class McpToolEnumerationService
    {
        private readonly ILogger<McpToolEnumerationService> _logger;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpToolEnumerationService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="mcpServerConfigurationService">The MCP tool server configuration service.</param>
        /// <param name="configuration">The application configuration.</param>
        public McpToolEnumerationService(
            ILogger<McpToolEnumerationService> logger,
            IMcpToolServerConfigurationService mcpServerConfigurationService,
            IConfiguration configuration)
        {
            _logger = logger;
            _mcpServerConfigurationService = mcpServerConfigurationService;
            _configuration = configuration;
        }

        /// <summary>
        /// Retrieves the authentication token for accessing MCP servers.
        /// </summary>
        /// <param name="userAuthorization">User authorization information.</param>
        /// <param name="authHandlerName">Name of the authentication handler.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="providedAuthToken">Optional pre-existing authentication token.</param>
        /// <returns>The authentication token.</returns>
        public async Task<string> GetAuthTokenAsync(
            UserAuthorization userAuthorization,
            string authHandlerName,
            ITurnContext turnContext,
            string? providedAuthToken = null)
        {
            if (!string.IsNullOrEmpty(providedAuthToken))
            {
                return providedAuthToken;
            }

            var token = await AgenticAuthenticationService.GetAgenticUserTokenAsync(
                userAuthorization,
                authHandlerName,
                turnContext,
                _configuration).ConfigureAwait(false);

            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Failed to obtain authentication token for MCP tool retrieval.");
            }

            return token;
        }

        /// <summary>
        /// Enumerates all MCP tools from configured servers for a given agent.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for MCP server access.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A tuple containing server configurations and a dictionary mapping server names to their available tools.</returns>
        public async Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(
            string agentInstanceId,
            string authToken,
            ITurnContext turnContext,
            ToolOptions toolOptions)
        {
            var toolsByServer = new Dictionary<string, IList<McpClientTool>>(StringComparer.OrdinalIgnoreCase);

            List<MCPServerConfig> servers;
            try
            {
                servers = await _mcpServerConfigurationService.ListToolServersAsync(
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

            foreach (var server in servers)
            {
                // Defensive validation of config object
                if (string.IsNullOrWhiteSpace(server.mcpServerName) || string.IsNullOrWhiteSpace(server.url))
                {
                    _logger.LogWarning(
                        "Skipping invalid MCP server config: Name='{Name}', Url='{Url}'",
                        server.mcpServerName,
                        server.url);
                    continue;
                }

                try
                {
                    var mcpTools = await _mcpServerConfigurationService.GetMcpClientToolsAsync(
                        turnContext,
                        server,
                        authToken,
                        toolOptions).ConfigureAwait(false);

                    toolsByServer[server.mcpServerName] = mcpTools;

                    _logger.LogInformation(
                        "Successfully loaded {ToolCount} tools from MCP server '{ServerName}'",
                        mcpTools.Count,
                        server.mcpServerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load tools from MCP server '{ServerName}' at '{Url}': {Error}",
                        server.mcpServerName,
                        server.url,
                        ex.Message);
                }
            }

            _logger.LogInformation(
                "MCP server discovery completed: Total servers={ServerCount}, Servers with tools={SuccessCount}",
                servers.Count,
                toolsByServer.Count);

            return (servers, toolsByServer);
        }

        /// <summary>
        /// Enumerates all MCP tools from configured servers, returning a flat list of all tools.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for MCP server access.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A flat list of all MCP tools from all configured servers.</returns>
        public async Task<IList<McpClientTool>> EnumerateAllToolsAsync(
            string agentInstanceId,
            string authToken,
            ITurnContext turnContext,
            ToolOptions toolOptions)
        {
            var (servers, toolsByServer) = await EnumerateToolsFromServersAsync(
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
