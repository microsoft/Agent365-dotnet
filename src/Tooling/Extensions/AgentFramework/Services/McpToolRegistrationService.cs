// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for registering and validating MCP tool servers for Agent Framework scenarios.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="IMcpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for logging.</param>
    /// <param name="mcpServerConfigurationService">MCP server configuration service.</param>
    /// <param name="configuration">Configuration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IMcpToolServerConfigurationService mcpServerConfigurationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _mcpServerConfigurationService = mcpServerConfigurationService;
        _configuration = configuration;
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

        if (authToken is null)
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

            // Use the shared enumeration service to get tools from all servers
            var (_, toolsByServer) = await _mcpServerConfigurationService.EnumerateToolsFromServersAsync(
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
            if (authToken is null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
            };

            // Use the shared enumeration service to get all tools
            var mcpTools = await _mcpServerConfigurationService.EnumerateAllToolsAsync(
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

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages, nameof(chatMessages));
        ArgumentNullException.ThrowIfNull(turnContext, nameof(turnContext));

        cancellationToken.ThrowIfCancellationRequested();

        return await SendChatHistoryAsync(chatMessages, turnContext, new ToolOptions
        {
            UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(chatMessages, nameof(chatMessages));
        ArgumentNullException.ThrowIfNull(turnContext, nameof(turnContext));
        ArgumentNullException.ThrowIfNull(toolOptions, nameof(toolOptions));

        try
        {
            // Convert to array to avoid multiple enumeration
            var chatMessageArray = chatMessages.ToArray();

            // Convert ChatMessage objects to ChatHistoryMessage array
            // Messages are used in the order provided (no sorting)
            // Empty arrays are sent to the MCP platform
            var chatHistoryMessages = chatMessageArray
                .Select(msg => new ChatHistoryMessage(
                    id: msg.MessageId ?? Guid.NewGuid().ToString(),
                    role: msg.Role.ToString(),
                    content: msg.Text ?? string.Empty,
                    timestamp: msg.CreatedAt ?? DateTimeOffset.UtcNow))
                .ToArray();

            _logger.LogInformation("Converted {MessageCount} chat messages to history format", chatHistoryMessages.Length);

            // Call the underlying service to send the chat history
            return await _mcpServerConfigurationService.SendChatHistoryAsync(
                turnContext,
                chatHistoryMessages,
                toolOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat history");
            return OperationResult.Failed(new OperationError(ex));
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessageStore, nameof(chatMessageStore));
        ArgumentNullException.ThrowIfNull(turnContext, nameof(turnContext));

        cancellationToken.ThrowIfCancellationRequested();

        return await SendChatHistoryAsync(chatMessageStore, turnContext, new ToolOptions
        {
            UserAgentConfiguration = Agent365AgentFrameworkSdkUserAgentConfiguration.Instance
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(chatMessageStore, nameof(chatMessageStore));
        ArgumentNullException.ThrowIfNull(turnContext, nameof(turnContext));
        ArgumentNullException.ThrowIfNull(toolOptions, nameof(toolOptions));

        try
        {
            // Retrieve messages from the store asynchronously
            var messages = await chatMessageStore.GetMessagesAsync(cancellationToken).ConfigureAwait(false);

            // Delegate to the IEnumerable overload
            return await SendChatHistoryAsync(messages, turnContext, toolOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve and send chat history from ChatMessageStore");
            return OperationResult.Failed(new OperationError(ex));
        }
    }
}
