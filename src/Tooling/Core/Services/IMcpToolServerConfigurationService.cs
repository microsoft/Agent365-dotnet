// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

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
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns the list of MCP Servers that are configured.</returns>
        Task<List<MCPServerConfig>> ListToolServers(string agentInstanceId, string environmentId, string authToken);

        /// <summary>
        /// Gets the MCP Client Tools from the specified MCP server.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="mCPServerConfig">The MCP server configuration.</param>
        /// <param name="environmentId">The environment ID.</param>
        /// <param name="authToken">The authentication token.</param>
        /// <returns>MCP Client Tools</returns>
        /// <exception cref="InvalidOperationException"></exception>
        Task<IList<McpClientTool>> GetMcpClientTools(ITurnContext turnContext, MCPServerConfig mCPServerConfig, string environmentId, string authToken);
    }
}
