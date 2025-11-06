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
    /// Creates a new AIAgent from the provided IChatClient with MCP tools added to existing tools.
    /// Returns the new agent instance configured with existing tools plus MCP tools.
    /// </summary>
    /// <param name="chatClient">The configured IChatClient to use for creating the agent.</param>
    /// <param name="agentInstructions">The agent instructions.</param>
    /// <param name="initialTools">The existing tools to keep and add MCP tools to.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="turnContext">Turn context for the current request</param>
    /// <param name="userAuthorization">User authorization information</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>A new AIAgent instance with existing tools plus MCP tools.</returns>
    Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null);
}