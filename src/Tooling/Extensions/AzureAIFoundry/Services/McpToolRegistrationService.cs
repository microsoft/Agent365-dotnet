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
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="mcpToolServerConfigurationService">The MCP tool server configuration service.</param>
    /// <param name="configuration">The application configuration.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IServiceProvider serviceProvider,
        IMcpToolServerConfigurationService mcpToolServerConfigurationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _mcpServerConfigurationService = mcpToolServerConfigurationService;
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

        if (authToken == null)
        {
            authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
        }

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

        List<MCPServerConfig> servers;
        try
        {
            servers = await _mcpServerConfigurationService.ListToolServersAsync(agentInstanceId, authToken, toolOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list MCP tool servers for AgentInstanceId={agentInstanceId}", agentInstanceId);
            return (new List<MCPToolDefinition>(), null);
        }

        if (servers.Count == 0)
        {
            _logger.LogInformation("No MCP servers configured for agentInstanceId={agentInstanceId}", agentInstanceId);
            return (new List<MCPToolDefinition>(), null);
        }

        // Collections we build for the return value
        var toolDefinitions = new List<MCPToolDefinition>();
        var discoveredTools = new Dictionary<string, IList<McpClientTool>>(StringComparer.OrdinalIgnoreCase);

        // Initialize combined tool resources early
        ToolResources? combinedToolResources = new ToolResources();

        foreach (var server in servers)
        {
            // Defensive validation of config object
            if (string.IsNullOrWhiteSpace(server.mcpServerName) || string.IsNullOrWhiteSpace(server.url))
            {
                _logger.LogWarning("Skipping invalid MCP server config: Name='{Name}', Url='{Url}'", server.mcpServerName, server.url);
                continue;
            }

            // TODO: to remove the "mcp_" prefix handling, after the fix from foundry team is rolled out.
            var server_label = server.mcpServerName.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase)
                ? server.mcpServerName.Substring(4)
                : server.mcpServerName;

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

            // Attempt live validation by connecting and listing tools; not used for updating the agentClient directly
            try
            {
                var mcpTools = await _mcpServerConfigurationService.GetMcpClientToolsAsync(turnContext, server, authToken, toolOptions);
                discoveredTools[server.mcpServerName] = mcpTools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed for MCP server {Server} at {Url}", server.mcpServerName, server.url);
            }
        }

        // Return null if no servers were processed successfully
        if (combinedToolResources.Mcp.Count == 0)
        {
            combinedToolResources = null;
        }

        // Summarize for caller - keep important summary logs
        _logger.LogInformation("MCP server discovery summary: Servers={ServerCount}, ToolDefinitions={ToolDefCount}", servers.Count, toolDefinitions.Count);
        foreach (var kvp in discoveredTools)
        {
            _logger.LogInformation(" - {Server}: {ToolCount} MCP tools", kvp.Key, kvp.Value.Count);
        }
        _logger.LogInformation("MCP tool definitions and resources prepared: Servers={ServerCount}, ToolDefinitions={ToolDefCount}, ResourceCount={ResourceCount}",
            servers.Count, toolDefinitions.Count, combinedToolResources?.Mcp.Count ?? 0);

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
        var chatHistoryMessages = messages.Select(message =>
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
        ArgumentNullException.ThrowIfNull(threadId);
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
        
        foreach (var contentItem in message.ContentItems)
        {
            // Check if the content item is MessageTextContent
            if (contentItem is MessageTextContent textContentItem)
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
        }

        return textContent.ToString();
    }
}