// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;

using Azure.AI.Agents.Persistent; // MCPToolDefinition, MCPToolResource, MCPApproval
using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Constants = Utils.Constants;

/// <summary>
/// Service for registering and validating MCP tool servers for Foundry (Persistent Agents) scenarios.
/// NOTE: Persistent Agents cannot be mutated to add tools after creation. This method therefore
/// performs discovery + validation and returns the MCP tool definitions and resources that a caller
/// SHOULD use when creating (or recreating) a PersistentAgent. No agent mutation is attempted here.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IServiceProvider _serviceProvider; // reserved for future DI expansion
    private readonly IMcpToolEnumerationService _mcpToolEnumerationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="mcpToolEnumerationService">The MCP tool enumeration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IServiceProvider serviceProvider,
        IMcpToolEnumerationService mcpToolEnumerationService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _mcpToolEnumerationService = mcpToolEnumerationService;
    }

    /// <summary>
    /// Add new MCP servers to the agent by updating the PersistentAgentsClient.
    /// </summary>
    /// <param name="agentClient">PersistentAgentsClient instance for the agent.</param>
    /// <param name="agentInstanceId">The ID of the agent instance.</param>
    /// <param name="userAuthorization">User authorization information.</param>
    /// <param name="turnContext">Turn context for the current request.</param>
    /// <param name="authToken">Authentication token for MCP server access.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        string agentInstanceId,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null)
    {
        if (agentClient == null)
        {
            throw new ArgumentNullException(nameof(agentClient));
        }

        try
        {
            // Get the tool definitions and resources using the internal implementation
            var (toolDefinitions, toolResources) = GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId, authToken ?? string.Empty, turnContext).GetAwaiter().GetResult();

            agentClient.Administration.UpdateAgent(
                agentInstanceId,
                tools: toolDefinitions,
                toolResources: toolResources);

            _logger.LogInformation("Successfully configured {Count} MCP tool servers for agent", toolDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure during MCP tool registration workflow for agent user {agentInstanceId}", agentInstanceId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AddToolServersToAgentAsync(
        PersistentAgentsClient agentClient,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null)
    {
        if (agentClient == null)
        {
            throw new ArgumentNullException(nameof(agentClient));
        }

        authToken = await _mcpToolEnumerationService.GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);

        var agenticAppId = turnContext.Activity.Recipient.AgenticAppId;

        try
        {
            // Perform the (potentially async) work in a dedicated task to keep this synchronous signature.
            var (toolDefinitions, toolResources) = GetMcpToolDefinitionsAndResourcesAsync(agenticAppId, authToken ?? string.Empty, turnContext).GetAwaiter().GetResult();

            agentClient.Administration.UpdateAgent(
                agenticAppId,
                tools: toolDefinitions,
                toolResources: toolResources);

            _logger.LogInformation("Successfully configured {Count} MCP tool servers for agent", toolDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure during MCP tool registration workflow for agent user {agenticAppId}", agenticAppId);
            throw;
        }
    }

    /// <summary>
    /// Get MCP tool definitions and resources.
    /// </summary>
    public async Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string authToken,
        ITurnContext turnContext)
    {
        // TODO: Make this method private
        // Tool resources should ideally be accessible via agentClient after AddToolServersToAgent.
        // This workaround is temporary and will be removed once the Foundry SDK correctly updates agentClient with tool resources.
        // Eventually, we should retrieve tool resources directly from agentClient.

        var toolOptions = new ToolOptions
        {
            UserAgentConfiguration = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance
        };

        // Use the shared enumeration service to get tools from all servers
        var (servers, toolsByServer) = await _mcpToolEnumerationService.EnumerateToolsFromServersAsync(
            agentInstanceId,
            authToken,
            turnContext,
            toolOptions).ConfigureAwait(false);

        if (servers.Count == 0)
        {
            _logger.LogInformation("No MCP servers configured for agentInstanceId={agentInstanceId}", agentInstanceId);
            return (new List<MCPToolDefinition>(), null);
        }

        // Collections we build for the return value
        var toolDefinitions = new List<MCPToolDefinition>();

        // Initialize combined tool resources early
        ToolResources? combinedToolResources = new ToolResources();

        // Create a lookup dictionary for faster server config access
        var serverLookup = servers.ToDictionary(s => s.mcpServerName, StringComparer.OrdinalIgnoreCase);

        foreach (var serverEntry in toolsByServer)
        {
            var mcpServerName = serverEntry.Key;
            var mcpTools = serverEntry.Value;

            if (!serverLookup.TryGetValue(mcpServerName, out var server) || string.IsNullOrWhiteSpace(server.url))
            {
                _logger.LogWarning("Could not find server config for '{ServerName}'", mcpServerName);
                continue;
            }

            // TODO: to remove the "mcp_" prefix handling, after the fix from foundry team is rolled out.
            var server_label = mcpServerName.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase)
                ? mcpServerName.Substring(4)
                : mcpServerName;

            // Build MCP tool artifacts (definition + resource w/headers) - this is the logic moved from MyAgent.cs
            var toolDef = new MCPToolDefinition(server_label, server.url);
            toolDefinitions.Add(toolDef);

            var resource = new MCPToolResource(server_label);

            // Set up authorization header
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                // Normalize bearer header (ensure it starts with "Bearer ")
                var headerValue = authToken.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase) ? authToken : $"{Constants.Headers.BearerPrefix} {authToken}";
                resource.UpdateHeader("Authorization", headerValue);
            }

            // Set up other headers
            resource.UpdateHeader("User-Agent", UserAgentHelper.BuildUserAgent(Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance));

            // Set approval requirement
            resource.RequireApproval = new MCPApproval("never");

            // Add directly to combined tool resources
            combinedToolResources.Mcp.Add(resource);

            _logger.LogInformation(" - {Server}: {ToolCount} MCP tools", mcpServerName, mcpTools.Count);
        }

        // Return null if no servers were processed successfully
        if (combinedToolResources.Mcp.Count == 0)
        {
            combinedToolResources = null;
        }

        _logger.LogInformation("MCP tool definitions and resources prepared: ToolDefinitions={ToolDefCount}, ResourceCount={ResourceCount}",
            toolDefinitions.Count, combinedToolResources?.Mcp.Count ?? 0);

        return (toolDefinitions, combinedToolResources);
    }
}