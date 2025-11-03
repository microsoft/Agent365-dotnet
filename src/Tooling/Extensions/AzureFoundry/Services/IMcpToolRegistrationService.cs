// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Azure.AI.Agents;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using System.Collections.Generic;

/// <summary>
/// Provides methods for managing MCP tool server registrations (Semantic Kernel independent).
/// </summary>
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Loads/initializes configured MCP tool servers for the specified agent/environment with full context.
    /// This is the primary method that customers should use in orchestrators with full authentication context.
    /// </summary>
    /// <param name="agentClient">The PersistentAgentsClient instance.</param>
    /// <param name="agentInstanceId">Agent Instance Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="userAuthorization">User authorization context.</param>
    /// <param name="turnContext">Turn context for the conversation.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        string agentInstanceId,
        string environmentId,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null);

    /// <summary>
    /// Get MCP tool definitions and resources asynchronously.
    /// </summary>
    /// <param name="agentInstanceId">Agent Instance Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="authToken">Auth token to access the MCP servers.</param>
    /// <param name="turnContext">Turn context for the conversation.</param>
    /// <returns>A tuple containing the list of MCP tool definitions and tool resources.</returns>
    Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string environmentId,
        string authToken,
        ITurnContext turnContext);
}