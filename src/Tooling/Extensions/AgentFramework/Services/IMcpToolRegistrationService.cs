// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Provides methods for managing MCP tool server registrations for Agent Framework scenarios.
/// </summary>
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Loads/initializes configured MCP tool servers for the specified agent/environment.
    /// This is the method that customers should use in orchestrators with hardcoded auth tokens.
    /// TODO: Remove once all orchestrators have full auth context.
    /// </summary>
    /// <param name="agent">The Agent Framework agent instance.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    void AddToolServersToAgent(
        object agent,
        string agentUserId,
        string environmentId,
        string? authToken = null);

    /// <summary>
    /// Loads/initializes configured MCP tool servers for the specified agent/environment with full context.
    /// This is the primary method that customers should use in orchestrators with full authentication context.
    /// </summary>
    /// <param name="agent">The Agent Framework agent instance.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="userAuthorization">User authorization context.</param>
    /// <param name="turnContext">Turn context for the conversation.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    void AddToolServersToAgent(
        object agent,
        string agentUserId,
        string environmentId,
        object userAuthorization,
        object turnContext,
        string? authToken = null);

    /// <summary>
    /// Get MCP tool definitions and functions asynchronously.
    /// </summary>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="authToken">Auth token to access the MCP servers.</param>
    /// <returns>A tuple containing the list of tool definitions and functions.</returns>
    Task<(IList<string> ToolDefinitions, IEnumerable<object> Functions)> GetMcpToolDefinitionsAndFunctionsAsync(
        string agentUserId,
        string environmentId,
        string authToken);
}