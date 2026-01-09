// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using ModelContextProtocol.Client;

namespace Microsoft.Agents.A365.Tooling.Services
{
	/// <summary>
	/// Defines the contract for enumerating MCP tools from configured servers.
	/// This service encapsulates the shared logic used across different agent framework implementations.
	/// </summary>
	public interface IMcpToolEnumerationService
	{
		/// <summary>
		/// Enumerates all MCP tools from configured servers, returning a flat list of all tools.
		/// </summary>
		/// <param name="agentInstanceId">The agent instance ID.</param>
		/// <param name="authToken">Authentication token for MCP server access.</param>
		/// <param name="turnContext">Turn context for the current request.</param>
		/// <param name="toolOptions">Tool options including user agent configuration.</param>
		/// <returns>A flat list of all MCP tools from all configured servers.</returns>
		Task<IList<McpClientTool>> EnumerateAllToolsAsync(string agentInstanceId, string authToken, ITurnContext turnContext, ToolOptions toolOptions);

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
		/// Retrieves the authentication token for accessing MCP servers.
		/// </summary>
		/// <param name="userAuthorization">User authorization information.</param>
		/// <param name="authHandlerName">Name of the authentication handler.</param>
		/// <param name="turnContext">Turn context for the current request.</param>
		/// <param name="providedAuthToken">Optional pre-existing authentication token.</param>
		/// <returns>The authentication token.</returns>
		Task<string> GetAuthTokenAsync(UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? providedAuthToken = null);
	}
}