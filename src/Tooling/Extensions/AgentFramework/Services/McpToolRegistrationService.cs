// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// Service for registering and validating MCP tool servers for Agent Framework scenarios.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mcpServerConfigurationService">The MCP tool server configuration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IMcpToolServerConfigurationService mcpServerConfigurationService)
    {
        _logger = logger;
        _mcpServerConfigurationService = mcpServerConfigurationService;
    }

    /// <inheritdoc />
    /// <summary>
    /// Add new MCP servers to the agent by creating a new Agent instance.
    /// 
    /// Note: Due to Microsoft.Extensions.AI framework limitations, MCP tools must be set during
    /// Agent creation. If new tools are found, this method creates a new Agent
    /// instance with all tools (existing + new) properly initialized.
    /// </summary>
    /// <param name="chatClient">The configured IChatClient to use for creating the agent</param>
    /// <param name="agentInstructions">The agent instructions</param>
    /// <param name="initialTools">The existing tools to add servers to</param>
    /// <param name="agentUserId">Agent User ID for the agent</param>
    /// <param name="environmentId">Environment ID for the environment</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authToken">Authentication token to access the MCP servers</param>
    /// <returns>New Agent instance with all MCP tools, or agent with original tools if no new servers</returns>
    public async Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        string environmentId,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient));
        }

        if (authToken == null)
        {
            authToken = AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, turnContext).GetAwaiter().GetResult();
        }

        try
        {
            // Step 2: Now update agent by adding MCP tools
            var updatedTools = new List<AITool>();
            
            // Keep any existing tools that were passed in
            updatedTools.AddRange(initialTools);

            // Get MCP tool server configurations
            var servers = await _mcpServerConfigurationService.ListToolServers(agentUserId, environmentId, authToken!);
            
            // Retrieve MCP tools from all configured servers
            foreach (var server in servers)
            {
                try
                {
                    var mcpTools = await _mcpServerConfigurationService.GetMcpClientTools(turnContext, server, environmentId, authToken);
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

            _logger.LogInformation("Loaded {McpCount} MCP tools for agent {AgentUserId} in environment {EnvironmentId}",
                updatedTools.Count, agentUserId, environmentId);

            // Create agent with updated tools (since AIAgent is immutable)
            var agentWithTools = chatClient.CreateAIAgent(
                instructions: agentInstructions,
                tools: [.. updatedTools]);

            // Return the enhanced agent
            return agentWithTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP tool servers for agent {AgentUserId} in environment {EnvironmentId}", 
                agentUserId, environmentId);
            throw;
        }
    }
}