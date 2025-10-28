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
    /// Enhances an existing IChatClient with MCP tools from configured tool servers.
    /// </summary>
    /// <param name="chatClient">The chat client to enhance with MCP tools.</param>
    /// <param name="agentUserId">Agent User Id for the agent.</param>
    /// <param name="environmentId">Environment Id for the environment.</param>
    /// <param name="authToken">Optional auth token to access the MCP servers.</param>
    /// <returns>A task that completes when MCP tools have been added to the chat client.</returns>
    Task AddToolServersToAgent(
        IChatClient chatClient,
        string agentUserId,
        string environmentId,
        string? authToken = null);
}