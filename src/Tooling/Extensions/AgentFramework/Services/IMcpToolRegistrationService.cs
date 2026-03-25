// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using Azure.AI.OpenAI;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
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
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>New Agent instance with all MCP tools, or agent with original tools if no new servers</returns>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a List of MCP tools to be added to the agent.
    /// </summary>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>List of AI Tools be added to an agent.</returns>
    Task<IList<AITool>> GetMcpToolsAsync(
        string agentUserId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Sends chat history from a ChatMessageStore to the MCP platform.
    /// </summary>
    /// <param name="chatMessageStore">The chat message store containing the conversation history. Empty stores are valid and will result in an empty array being forwarded to the MCP platform.</param>
    /// <param name="turnContext">Turn context for the current request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An OperationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatMessageStore"/> or <paramref name="turnContext"/> is null.
    /// </exception>
    /// <remarks>
    /// Empty message stores are passed through to the MCP platform rather than being short-circuited.
    /// This ensures the platform call is always made, allowing the platform to handle empty states as needed.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history from a ChatMessageStore to the MCP platform.
    /// </summary>
    /// <param name="chatMessageStore">The chat message store containing the conversation history. Empty stores are valid and will result in an empty array being forwarded to the MCP platform.</param>
    /// <param name="turnContext">Turn context for the current request.</param>
    /// <param name="toolOptions">Tool options for configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An OperationResult indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chatMessageStore"/>, <paramref name="turnContext"/>, or <paramref name="toolOptions"/> is null.
    /// </exception>
    /// <remarks>
    /// Empty message stores are passed through to the MCP platform rather than being short-circuited.
    /// This ensures the platform call is always made, allowing the platform to handle empty states as needed.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        ChatMessageStore chatMessageStore,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}