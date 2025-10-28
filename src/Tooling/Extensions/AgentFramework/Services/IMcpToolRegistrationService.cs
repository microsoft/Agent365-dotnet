// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

/// <summary>
/// Provides methods for managing MCP tool server registrations for Agent Framework scenarios.
/// </summary>
public interface IMcpToolRegistrationService
{
    /// <summary>
    /// Takes an existing AIAgent and creates a new AIAgent with MCP tools added to its existing tools.
    /// Returns the new agent instance since AIAgent is immutable.
    /// </summary>
    /// <param name="agentClient">The Azure OpenAI client for creating the new AIAgent.</param>
    /// <param name="agentInstructions">The agent instructions.</param>
    /// <param name="agent">The existing AIAgent to enhance with MCP tools.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>A new AIAgent instance with the original tools plus MCP tools.</returns>
    Task<AIAgent> AddToolServersToAgent(
        AzureOpenAIClient agentClient,
        string agentInstructions,
        AIAgent agent,
        string agentUserId,
        string environmentId,
        string? authToken = null);
}