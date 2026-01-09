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
    private readonly IMcpToolEnumerationService _mcpToolEnumerationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mcpToolEnumerationService">The MCP tool enumeration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IMcpToolEnumerationService mcpToolEnumerationService)
    {
        _logger = logger;
        _mcpToolEnumerationService = mcpToolEnumerationService;
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

        authToken = await _mcpToolEnumerationService.GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);

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

            // Use the shared enumeration service to get tools from all servers
            var (servers, toolsByServer) = await _mcpToolEnumerationService.EnumerateToolsFromServersAsync(
                agentUserId,
                authToken,
                turnContext,
                toolOptions).ConfigureAwait(false);

            // Add all MCP tools from all servers
            foreach (var serverEntry in toolsByServer)
            {
                updatedTools.AddRange(serverEntry.Value.Cast<AITool>());
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
        try
        {
            authToken = await _mcpToolEnumerationService.GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
            };

            // Use the shared enumeration service to get all tools
            var mcpTools = await _mcpToolEnumerationService.EnumerateAllToolsAsync(
                agentUserId,
                authToken,
                turnContext,
                toolOptions).ConfigureAwait(false);

            // Convert to AITool list
            var a365ToolList = mcpTools.Cast<AITool>().ToList();

            _logger.LogInformation("Loaded {McpCount} MCP tools for agent {AgentUserId}",
                a365ToolList.Count, agentUserId);

            return a365ToolList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP tool servers for agent {AgentUserId}",
                agentUserId);
            throw;
        }
    }
}