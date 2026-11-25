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
        /// <remarks>
        /// The MCP client that serves the returned tools isn't disposed, because the tools use it to call the server.
        /// To dispose the clients when the tools are no longer used, use
        /// <see cref="GetMcpToolSetAsync(string, string, IMcpTokenProvider, ITurnContext, ToolOptions, CancellationToken)"/>.
        /// </remarks>
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
        /// <remarks>
        /// The MCP clients that serve the returned tools aren't disposed.
        /// </remarks>
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
        /// <remarks>
        /// The MCP clients that serve the returned tools aren't disposed. To dispose them when the tools are no longer used, use
        /// <see cref="GetMcpToolSetAsync(string, string, IMcpTokenProvider, ITurnContext, ToolOptions, CancellationToken)"/>.
        /// </remarks>
        Task<(List<MCPServerConfig> Servers, Dictionary<string, IList<McpClientTool>> ToolsByServer)> EnumerateToolsFromServersAsync(string agentInstanceId, string authToken, IMcpTokenProvider tokenProvider, ITurnContext turnContext, ToolOptions toolOptions);

        /// <summary>
        /// Enumerates the MCP tools from the servers configured for the agent, acquiring per-audience tokens for each server,
        /// and returns them together with the MCP clients that serve them.
        /// V2 servers (distinct audience) receive audience-scoped tokens via <paramref name="tokenProvider"/>;
        /// V1 servers fall back to <paramref name="authToken"/>.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Shared authentication token (V1 fallback).</param>
        /// <param name="tokenProvider">Provides per-server Bearer tokens, routing V2 servers to audience-scoped tokens.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the enumeration.</param>
        /// <returns>
        /// An <see cref="McpToolSet"/> with the configured servers, the tools of each server that responded, and the MCP clients
        /// that serve the tools. Dispose it when the tools are no longer used, for example at the end of the turn.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Each tool's MCP client sends the request headers, including the <c>Authorization</c> token, of this call.
        /// Use the tool set for the current turn only, and dispose it when the turn ends. Servers whose tools can't be
        /// listed are skipped and logged, and their clients are disposed.
        /// </para>
        /// <para>
        /// The default implementation returns the tools from
        /// <see cref="EnumerateToolsFromServersAsync(string, string, IMcpTokenProvider, ITurnContext, ToolOptions)"/> in a tool set
        /// that doesn't dispose their clients. <see cref="McpToolServerConfigurationService"/> returns a tool set that disposes them.
        /// </para>
        /// </remarks>
        async Task<McpToolSet> GetMcpToolSetAsync(string agentInstanceId, string authToken, IMcpTokenProvider tokenProvider, ITurnContext turnContext, ToolOptions toolOptions, CancellationToken cancellationToken = default)
        {
            var (servers, toolsByServer) = await EnumerateToolsFromServersAsync(agentInstanceId, authToken, tokenProvider, turnContext, toolOptions).ConfigureAwait(false);
            return new McpToolSet(
                servers,
                toolsByServer.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<McpClientTool>)entry.Value.ToList(), toolsByServer.Comparer));
        }

        /// <summary>
        /// Enumerates all MCP tools from configured servers, returning a flat list of all tools.
        /// </summary>
        /// <param name="agentInstanceId">The agent instance ID.</param>
        /// <param name="authToken">Authentication token for MCP server access.</param>
        /// <param name="turnContext">Turn context for the current request.</param>
        /// <param name="toolOptions">Tool options including user agent configuration.</param>
        /// <returns>A flat list of all MCP tools from all configured servers.</returns>
        /// <remarks>
        /// The MCP clients that serve the returned tools aren't disposed.
        /// </remarks>
        Task<IList<McpClientTool>> EnumerateAllToolsAsync(string agentInstanceId, string authToken, ITurnContext turnContext, ToolOptions toolOptions);
    }
}
