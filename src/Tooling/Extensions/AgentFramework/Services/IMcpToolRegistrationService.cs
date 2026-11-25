// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides methods for managing MCP tool server registrations for Agent Framework scenarios.
/// </summary>
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Add new MCP servers to the agent by creating a new Agent instance.
    /// 
    /// Note: Due to Microsoft.Extensions.AI framework limitations, MCP tools must be set during
    /// Agent creation. If new tools are found, this method creates a new Agent
    /// instance with all tools (existing + new) properly initialized.
    /// </summary>
    /// <param name="chatClient">The configured IChatClient to use for creating the agent.</param>
    /// <param name="agentInstructions">The agent instructions.</param>
    /// <param name="initialTools">The existing tools to keep and add MCP tools to.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>New Agent instance with all MCP tools, or agent with original tools if no new servers</returns>
    /// <remarks>
    /// The MCP clients that serve the agent's MCP tools aren't disposed. For an agent used for one turn, use
    /// <see cref="GetMcpToolSetAsync"/> to get the tools together with their clients, build the agent from them,
    /// and dispose the tool set when the turn ends.
    /// </remarks>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Returns a List of MCP tools to be added to the agent.
    /// </summary>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>List of AI Tools be added to an agent.</returns>
    /// <remarks>
    /// The MCP clients that serve the returned tools aren't disposed. To get tools for one turn and dispose their
    /// clients when the turn ends, use <see cref="GetMcpToolSetAsync"/>.
    /// </remarks>
    Task<IList<AITool>> GetMcpToolsAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Returns the MCP tools for the current turn together with the MCP clients that serve them.
    /// </summary>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <param name="cancellationToken">A cancellation token to cancel tool discovery.</param>
    /// <returns>
    /// An <see cref="McpToolSet"/> that enumerates the MCP tools as <see cref="AITool"/> instances. Dispose it when the turn ends.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each tool calls its MCP server through a client that sends the request headers, including the <c>Authorization</c>
    /// token, of this call. Use the tools for the current turn only, and dispose the tool set when the turn ends,
    /// for example with <c>await using</c>. Disposing it closes the clients' connections and ends their MCP sessions;
    /// tools from a disposed tool set can't be invoked. Don't cache the tools across turns.
    /// </para>
    /// <code>
    /// await using var mcpTools = await toolService.GetMcpToolSetAsync(agentUserId, userAuthorization, authHandlerName, turnContext);
    /// var agent = chatClient.AsAIAgent(instructions: instructions, tools: [.. localTools, .. mcpTools]);
    /// // Run the agent for this turn.
    /// </code>
    /// <para>
    /// The default implementation throws <see cref="NotSupportedException"/>. <see cref="McpToolRegistrationService"/> implements it.
    /// </para>
    /// </remarks>
    Task<McpToolSet> GetMcpToolSetAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null,
        CancellationToken cancellationToken = default)
        => Task.FromException<McpToolSet>(new NotSupportedException(
            $"{GetType().Name} doesn't return MCP tools together with their clients. Use {nameof(GetMcpToolsAsync)} instead."));

    /// <summary>
    /// Sends chat history to the MCP platform.
    /// </summary>
    /// <param name="chatMessages">The chat messages to send. Empty collections are valid and will be forwarded to the MCP platform.</param>
    /// <param name="turnContext">Turn context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An OperationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatMessages"/> or <paramref name="turnContext"/> is null.
    /// </exception>
    /// <remarks>
    /// Empty message collections are passed through to the MCP platform rather than being short-circuited.
    /// This ensures the platform call is always made, allowing the platform to handle empty states as needed.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history to the MCP platform.
    /// </summary>
    /// <param name="chatMessages">The chat messages to send. Empty collections are valid and will be forwarded to the MCP platform.</param>
    /// <param name="turnContext">Turn context for the current request.</param>
    /// <param name="toolOptions">Tool options for configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An OperationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatMessages"/>, <paramref name="turnContext"/>, or <paramref name="toolOptions"/> is null.
    /// </exception>
    /// <remarks>
    /// Empty message collections are passed through to the MCP platform rather than being short-circuited.
    /// This ensures the platform call is always made, allowing the platform to handle empty states as needed.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        IEnumerable<ChatMessage> chatMessages,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);

}