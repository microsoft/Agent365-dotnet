// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticMcpTokenProvider = Microsoft.Agents.A365.Tooling.Services.AgenticMcpTokenProvider;
using Constants = Microsoft.Agents.A365.Tooling.Utils.Constants;

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
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="mcpServerConfigurationService">The MCP server configuration service.</param>
    /// <param name="configuration">The configuration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IServiceProvider serviceProvider,
        IMcpToolServerConfigurationService mcpServerConfigurationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _mcpServerConfigurationService = mcpServerConfigurationService;
        _configuration = configuration;
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

        if (authToken is null)
        {
            authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
        }

        var agenticAppId = turnContext.Activity.Recipient.AgenticAppId;

        try
        {
            // Use V2-aware overload so per-audience tokens are applied to each server.
            var (toolDefinitions, toolResources) = await GetMcpToolDefinitionsAndResourcesAsync(
                agenticAppId, authToken ?? string.Empty, turnContext, userAuthorization, authHandlerName).ConfigureAwait(false);

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

    /// <inheritdoc />
    public Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string authToken,
        ITurnContext turnContext)
        => GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId, authToken, turnContext, userAuthorization: null, authHandlerName: null);

    /// <summary>
    /// Get MCP tool definitions and resources, optionally using per-audience tokens for V2 servers.
    /// </summary>
    /// <param name="agentInstanceId">Agent instance ID.</param>
    /// <param name="authToken">Shared auth token (V1 fallback).</param>
    /// <param name="turnContext">Turn context for the request.</param>
    /// <param name="userAuthorization">When provided together with <paramref name="authHandlerName"/>, enables per-audience token acquisition for V2 servers.</param>
    /// <param name="authHandlerName">Auth handler name used with <paramref name="userAuthorization"/>.</param>
    public async Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string authToken,
        ITurnContext turnContext,
        UserAuthorization? userAuthorization,
        string? authHandlerName)
    {
        // TODO: Make this method private
        // Tool resources should ideally be accessible via agentClient after AddToolServersToAgent.
        // This workaround is temporary and will be removed once the Foundry SDK correctly updates agentClient with tool resources.
        // Eventually, we should retrieve tool resources directly from agentClient.

        var toolOptions = new ToolOptions
        {
            UserAgentConfiguration = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance
        };

        List<MCPServerConfig> servers;
        Dictionary<string, IList<McpClientTool>> toolsByServer;

        // When caller supplies auth objects, use per-audience tokens (V2-aware path).
        if (userAuthorization is not null && authHandlerName is not null)
        {
            var tokenProvider = new AgenticMcpTokenProvider(
                userAuthorization, authHandlerName, turnContext, _configuration, _logger);

            var concreteService = _mcpServerConfigurationService as McpToolServerConfigurationService;
            (servers, toolsByServer) = concreteService is not null
                ? await concreteService.EnumerateToolsFromServersAsync(agentInstanceId, authToken, tokenProvider, turnContext, toolOptions).ConfigureAwait(false)
                : await _mcpServerConfigurationService.EnumerateToolsFromServersAsync(agentInstanceId, authToken, turnContext, toolOptions).ConfigureAwait(false);
        }
        else
        {
            // Fallback: V1 path — all servers share the single authToken.
            (servers, toolsByServer) = await _mcpServerConfigurationService.EnumerateToolsFromServersAsync(
                agentInstanceId,
                authToken,
                turnContext,
                toolOptions).ConfigureAwait(false);
        }

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

            // Set up authorization header.
            // Prefer the per-server token injected by AttachPerAudienceTokensAsync (V2),
            // fall back to the shared authToken for V1 servers.
            var rawToken = server.Headers is not null &&
                           server.Headers.TryGetValue(Constants.Headers.Authorization, out var perServerHeader) &&
                           !string.IsNullOrWhiteSpace(perServerHeader)
                ? perServerHeader
                : authToken;

            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                // Normalize to "Bearer <token>"
                var headerValue = rawToken.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase)
                    ? rawToken
                    : $"{Constants.Headers.BearerPrefix} {rawToken}";
                resource.UpdateHeader(Constants.Headers.Authorization, headerValue);
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

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        PersistentThreadMessage[] messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(turnContext);
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        var toolOptions = new ToolOptions
        {
            UserAgentConfiguration = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance
        };

        return await SendChatHistoryAsync(turnContext, messages, toolOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        PersistentThreadMessage[] messages,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(turnContext);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(toolOptions);
        cancellationToken.ThrowIfCancellationRequested();

        // Convert PersistentThreadMessage[] to ChatHistoryMessage[]
        var chatHistoryMessages = messages
            .Where(message =>
            {
                // Validate message properties and skip invalid messages
                if (message == null)
                {
                    _logger.LogWarning("Skipping null message");
                    return false;
                }
                if (message.Id == null)
                {
                    _logger.LogWarning("Skipping message with null Id");
                    return false;
                }
                if (message.Role == null)
                {
                    _logger.LogWarning("Skipping message with null Role (Id: {MessageId})", message.Id);
                    return false;
                }
                return true;
            })
            .Select(message =>
            {
                // Extract text content from ContentItems
                var content = ExtractContentFromMessage(message);
                
                // CreatedAt is already a DateTimeOffset in Azure.AI.Agents.Persistent
                var timestamp = message.CreatedAt;
                
                return new ChatHistoryMessage(
                    id: message.Id,
                    role: message.Role.ToString().ToLowerInvariant(),
                    content: content,
                    timestamp: timestamp
                );
            }).ToArray();

        return await _mcpServerConfigurationService.SendChatHistoryAsync(
            turnContext,
            chatHistoryMessages,
            toolOptions,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        PersistentAgentsClient agentClient,
        string threadId,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentNullException.ThrowIfNull(turnContext);
        cancellationToken.ThrowIfCancellationRequested();

        var toolOptions = new ToolOptions
        {
            UserAgentConfiguration = Agent365AzureAIFoundrySdkUserAgentConfiguration.Instance
        };

        return await SendChatHistoryAsync(agentClient, threadId, turnContext, toolOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        PersistentAgentsClient agentClient,
        string threadId,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        ArgumentNullException.ThrowIfNull(turnContext);
        ArgumentNullException.ThrowIfNull(toolOptions);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Retrieve messages from Azure AI Foundry
            var messages = new List<PersistentThreadMessage>();
            
            await foreach (var message in agentClient.Messages.GetMessagesAsync(threadId, cancellationToken: cancellationToken))
            {
                messages.Add(message);
            }

            _logger.LogInformation("Retrieved {MessageCount} messages from thread {ThreadId}", messages.Count, threadId);

            // Delegate to the overload that accepts PersistentThreadMessage[] directly
            return await SendChatHistoryAsync(turnContext, messages.ToArray(), toolOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SendChatHistoryAsync operation was canceled for thread {ThreadId}", threadId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat history for thread {ThreadId}: {Message}", threadId, ex.Message);
            return OperationResult.Failed(new OperationError(ex));
        }
    }

    /// <summary>
    /// Extracts text content from a PersistentThreadMessage.
    /// </summary>
    /// <param name="message">The message to extract content from.</param>
    /// <returns>The extracted text content, or an empty string if no text content is found.</returns>
    private string ExtractContentFromMessage(PersistentThreadMessage message)
    {
        if (message.ContentItems == null || message.ContentItems.Count == 0)
        {
            return string.Empty;
        }

        var textContent = new System.Text.StringBuilder();
        
        foreach (var textContentItem in message.ContentItems.OfType<MessageTextContent>())
        {
            if (!string.IsNullOrEmpty(textContentItem.Text))
            {
                if (textContent.Length > 0)
                {
                    textContent.Append(" ");
                }
                textContent.Append(textContentItem.Text);
            }
        }

        return textContent.ToString();
    }
}