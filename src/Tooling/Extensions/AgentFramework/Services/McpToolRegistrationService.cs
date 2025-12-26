// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;
using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Service for registering and validating MCP tool servers for Agent Framework scenarios.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mcpServerConfigurationService">The MCP tool server configuration service.</param>
    /// <param name="configuration">The application configuration.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IMcpToolServerConfigurationService mcpServerConfigurationService,
        IConfiguration configuration)
    {
        _configuration = configuration;
        _logger = logger;
        _mcpServerConfigurationService = mcpServerConfigurationService;
    }

    /// <inheritdoc />
    public async Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient));
        }

        if (authToken == null)
        {
            authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
        }

        try
        {
            // Step 2: Now update agent by adding MCP tools
            var updatedTools = new List<AITool>();

            // Keep any existing tools that were passed in
            updatedTools.AddRange(initialTools);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
            };

            // Get MCP tool server configurations
            var servers = await _mcpServerConfigurationService.ListToolServersAsync(agentUserId, authToken!, toolOptions).ConfigureAwait(false);

            // Retrieve MCP tools from all configured servers
            foreach (var server in servers)
            {
                try
                {
                    var mcpTools = await _mcpServerConfigurationService.GetMcpClientToolsAsync(turnContext, server, authToken, toolOptions).ConfigureAwait(false);
                    // Add the MCP tools
                    updatedTools.AddRange(mcpTools.Cast<AITool>());

                    _logger.LogInformation("Successfully loaded {ToolCount} tools from MCP server '{ServerName}'",
                        mcpTools.Count, server.mcpServerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tools from MCP server '{ServerName}': {Error}",
                        server.mcpServerName, ex.Message);
                }
            }

            _logger.LogInformation("Loaded {McpCount} MCP tools for agent {AgentUserId}",
                updatedTools.Count, agentUserId);

            // Create agent with updated tools (since AIAgent is immutable)
            var agentWithTools = chatClient.CreateAIAgent(
                instructions: agentInstructions,
                tools: [.. updatedTools]);

            // Return the enhanced agent
            return agentWithTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP tool servers for agent {AgentUserId}",
                agentUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<AITool>> GetMcpToolsAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        var a365ToolList = new List<AITool>();
        try
        {
            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
                if (authToken == null )
                {
                    throw new InvalidOperationException("Failed to obtain authentication token for MCP tool retrieval.");
                }
            }

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
            };

            // Get MCP tool server configurations
            var servers = await _mcpServerConfigurationService.ListToolServersAsync(agentUserId, authToken!, toolOptions).ConfigureAwait(false);

            // Retrieve MCP tools from all configured servers
            foreach (var server in servers)
            {
                try
                {
                    var mcpTools = await _mcpServerConfigurationService.GetMcpClientToolsAsync(turnContext, server, authToken, toolOptions).ConfigureAwait(false);
                    // Add the MCP tools
                    a365ToolList.AddRange(mcpTools.Cast<AITool>());

                    _logger.LogInformation("Successfully loaded {ToolCount} tools from MCP server '{ServerName}'",
                        mcpTools.Count, server.mcpServerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tools from MCP server '{ServerName}': {Error}",
                        server.mcpServerName, ex.Message);
                }
            }

            _logger.LogInformation("Loaded {McpCount} MCP tools for agent {AgentUserId}",
                a365ToolList.Count, agentUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP tool servers for agent {AgentUserId}",
                agentUserId);
            throw;
        }
        return a365ToolList;
    }
}