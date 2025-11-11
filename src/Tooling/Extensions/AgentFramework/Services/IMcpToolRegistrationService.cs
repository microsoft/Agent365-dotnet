// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
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
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authHandlerName">Authentication Handler Name for use with the UserAuthorization System</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>New Agent instance with all MCP tools, or agent with original tools if no new servers</returns>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        string environmentId,
        UserAuthorization userAuthorization,
        string authHandlerName,
        ITurnContext turnContext,
        string? authToken = null);
}