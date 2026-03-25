// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Azure.AI.Agents;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Provides methods for managing MCP tool server registrations (Semantic Kernel independent).
/// </summary>
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Loads/initializes configured MCP tool servers for the specified agent with full context.
    /// This is the primary method that customers should use in orchestrators with full authentication context.
    /// </summary>
    /// <param name="agentClient">The PersistentAgentsClient instance.</param>
    /// <param name="userAuthorization">User authorization context.</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="turnContext">Turn context for the conversation.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    Task AddToolServersToAgentAsync(
        PersistentAgentsClient agentClient,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get MCP tool definitions and resources asynchronously.
    /// </summary>
    /// <param name="agentInstanceId">Agent Instance Id for the agent.</param>
    /// <param name="authToken">Auth token to access the MCP servers.</param>
    /// <param name="turnContext">Turn context for the conversation.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A tuple containing the list of MCP tool definitions and tool resources.</returns>
    Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string authToken,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history to the MCP platform for real-time threat protection.
    /// Messages are provided directly by the caller as Azure AI Foundry messages.
    /// </summary>
    /// <param name="turnContext">The turn context containing conversation information.</param>
    /// <param name="messages">The Azure AI Foundry persistent thread messages to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="messages"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method converts PersistentThreadMessage objects to the ChatHistoryMessage format and sends them to the MCP platform.
    /// The CreatedAt timestamp from Azure AI Foundry is preserved for each message.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        PersistentThreadMessage[] messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history to the MCP platform for real-time threat protection.
    /// Messages are provided directly by the caller as Azure AI Foundry messages.
    /// </summary>
    /// <param name="turnContext">The turn context containing conversation information.</param>
    /// <param name="messages">The Azure AI Foundry persistent thread messages to send.</param>
    /// <param name="toolOptions">Tool options for sending chat history.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/>, <paramref name="messages"/>, or <paramref name="toolOptions"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method converts PersistentThreadMessage objects to the ChatHistoryMessage format and sends them to the MCP platform.
    /// The CreatedAt timestamp from Azure AI Foundry is preserved for each message.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        ITurnContext turnContext,
        PersistentThreadMessage[] messages,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history to the MCP platform for real-time threat protection.
    /// Messages are retrieved from the Azure AI Foundry Persistent Agents client.
    /// </summary>
    /// <param name="agentClient">The PersistentAgentsClient instance to retrieve messages from.</param>
    /// <param name="threadId">The thread ID containing the messages to send.</param>
    /// <param name="turnContext">The turn context containing conversation information.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="agentClient"/>, <paramref name="threadId"/>, or <paramref name="turnContext"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method retrieves messages from the Azure AI Foundry Persistent Agents client using the specified thread ID,
    /// converts them to the ChatHistoryMessage format, and sends them to the MCP platform.
    /// The CreatedAt timestamp from Azure AI Foundry is preserved for each message.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        PersistentAgentsClient agentClient,
        string threadId,
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat history to the MCP platform for real-time threat protection.
    /// Messages are retrieved from the Azure AI Foundry Persistent Agents client.
    /// </summary>
    /// <param name="agentClient">The PersistentAgentsClient instance to retrieve messages from.</param>
    /// <param name="threadId">The thread ID containing the messages to send.</param>
    /// <param name="turnContext">The turn context containing conversation information.</param>
    /// <param name="toolOptions">Tool options for sending chat history.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="agentClient"/>, <paramref name="threadId"/>, <paramref name="turnContext"/>, or <paramref name="toolOptions"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via the <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// This method retrieves messages from the Azure AI Foundry Persistent Agents client using the specified thread ID,
    /// converts them to the ChatHistoryMessage format, and sends them to the MCP platform.
    /// The CreatedAt timestamp from Azure AI Foundry is preserved for each message.
    /// </remarks>
    Task<OperationResult> SendChatHistoryAsync(
        PersistentAgentsClient agentClient,
        string threadId,
        ITurnContext turnContext,
        ToolOptions toolOptions,
        CancellationToken cancellationToken = default);
}