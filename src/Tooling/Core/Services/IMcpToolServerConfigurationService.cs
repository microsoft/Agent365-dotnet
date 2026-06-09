// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// <exception cref="Microsoft.Agents.A365.Tooling.McpConnectionsRequiredException">Thrown when configured MCP servers report missing downstream connections (aggregate connectivity status other than "Ready").</exception>
        Task<List<MCPServerConfig>> ListToolServersAsync(string agentInstanceId, string authToken);

        /// <summary>
        /// Gets the list of MCP Servers that are configured for the agent.
        /// </summary>
        /// <param name="agentInstanceId">Agent instance Id for the agent.</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <param name="toolOptions">Tool options for listing servers.</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        /// <exception cref="Microsoft.Agents.A365.Tooling.McpConnectionsRequiredException">Thrown when configured MCP servers report missing downstream connections (aggregate connectivity status other than "Ready").</exception>
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
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="chatHistoryMessages"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when required turn context properties (Conversation.Id, Activity.Id, or Activity.Text) are null.</exception>
        /// <remarks>
        /// HTTP exceptions (HttpRequestException, TaskCanceledException) are caught and logged but not rethrown.
        /// Instead, the method returns an <see cref="OperationResult"/> indicating whether the operation succeeded or failed.
        /// Callers can choose to inspect the result for error handling or ignore it if error details are not needed.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends chat history to the MCP platform for real-time threat protection.
        /// </summary>
        /// <param name="turnContext">The turn context containing conversation information.</param>
        /// <param name="chatHistoryMessages">The chat history messages to send.</param>
        /// <param name="toolOptions">Tool options for sending chat history.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation that returns an <see cref="OperationResult"/> indicating success or failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="turnContext"/> or <paramref name="chatHistoryMessages"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when required turn context properties (Conversation.Id, Activity.Id, or Activity.Text) are null.</exception>
        /// <remarks>
        /// HTTP exceptions (HttpRequestException, TaskCanceledException) are caught and logged but not rethrown.
        /// Instead, the method returns an <see cref="OperationResult"/> indicating whether the operation succeeded or failed.
        /// Callers can choose to inspect the result for error handling or ignore it if error details are not needed.
        /// </remarks>
        Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistoryMessage[] chatHistoryMessages, ToolOptions toolOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates all MCP tools from configured servers for a given agent.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for MCP server access.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A tuple containing server configurations and a dictionary mapping server names to their available tools.</returns>
        Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(string agentInstanceId, string authToken, ITurnContext turnContext, ToolOptions toolOptions);

        /// <summary>
        /// Enumerates all MCP tools from configured servers, acquiring per-audience tokens for each server.
        /// V2 servers (distinct audience) receive audience-scoped tokens via <paramref name="tokenProvider"/>;
        /// V1 servers fall back to <paramref name="authToken"/>.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Shared authentication token (V1 fallback).</param>
        /// <param name="tokenProvider">Provides per-server Bearer tokens, routing V2 servers to audience-scoped tokens.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A tuple containing server configurations and a dictionary mapping server names to their available tools.</returns>
        Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(string agentInstanceId, string authToken, IMcpTokenProvider tokenProvider, ITurnContext turnContext, ToolOptions toolOptions);

        /// <summary>
        /// Enumerates all MCP tools from configured servers, returning a flat list of all tools.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for MCP server access.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A flat list of all MCP tools from all configured servers.</returns>
        Task<IList<McpClientTool>> EnumerateAllToolsAsync(string agentInstanceId, string authToken, ITurnContext turnContext, ToolOptions toolOptions);
    }
}
