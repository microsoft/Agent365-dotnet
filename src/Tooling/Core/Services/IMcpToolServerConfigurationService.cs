// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Runtime;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder;
using ModelContextProtocol.Client;

namespace Microsoft.Agents.A365.Tooling.Services
{
    /// <summary>
    /// Provides methods for managing MCP server configurations.
    /// </summary>
    public interface IMcpToolServerConfigurationService
    {
        /// <summary>
        /// Gets the list of MCP Servers that are configured for the agent.
        /// </summary>
        /// <param name="agentInstanceId">Agent instance Id for the agent.</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken);

        /// <summary>
        /// Gets the list of MCP Servers that are configured for the agent.
        /// </summary>
        /// <param name="agentInstanceId">Agent instance Id for the agent.</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <param name="toolOptions">Tool options for listing servers.</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken, ToolOptions toolOptions);

        /// <summary>
        /// Gets the MCP Client Tools from the specified MCP server.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="mCPServerConfig">The MCP server configuration.</param>
        /// <param name="authToken">The authentication token.</param>
        /// <param name="toolOptions">Tool options for listing servers.</param>
        /// <returns>MCP Client Tools</returns>
        /// <exception cref="InvalidOperationException"></exception>
        Task<IList<McpClientTool>> GetMcpClientToolsAsync(ITurnContext turnContext, MCPServerConfig mCPServerConfig, string authToken, ToolOptions toolOptions);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistoryMessages">The chat history messages to send.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="chatHistoryMessages"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when required turn context properties (Conversation.Id, Activity.Id, or Activity.Text) are null.</exception>
        /// <remarks>
        /// HTTP exceptions (HttpRequestException, TaskCanceledException) are caught and logged but not rethrown.
        /// Instead, the method returns an <see cref="OperationResult"/> indicating whether the operation succeeded or failed.
        /// Callers can choose to inspect the result for error handling or ignore it if error details are not needed.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistoryMessages">The chat history messages to send.</param>
        /// <param name="toolOptions">Tool options for sending chat history.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="chatHistoryMessages"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when required turn context properties (Conversation.Id, Activity.Id, or Activity.Text) are null.</exception>
        /// <remarks>
        /// HTTP exceptions (HttpRequestException, TaskCanceledException) are caught and logged but not rethrown.
        /// Instead, the method returns an <see cref="OperationResult"/> indicating whether the operation succeeded or failed.
        /// Callers can choose to inspect the result for error handling or ignore it if error details are not needed.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages, ToolOptions toolOptions);
    }
}
