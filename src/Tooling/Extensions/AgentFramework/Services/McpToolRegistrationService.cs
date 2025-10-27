// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Net.Http;

/// <summary>
/// Service for registering and validating MCP tool servers for Agent Framework scenarios.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public McpToolRegistrationService(ILogger<IMcpToolRegistrationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void AddToolServersToAgent(
        object agent,
        string agentUserId,
        string environmentId,
        string? authToken = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void AddToolServersToAgent(
        object agent,
        string agentUserId,
        string environmentId,
        object userAuthorization,
        object turnContext,
        string? authToken = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<(IList<string> ToolDefinitions, IEnumerable<object> Functions)> GetMcpToolDefinitionsAndFunctionsAsync(
        string agentUserId,
        string environmentId,
        string authToken)
    {
        throw new NotImplementedException();
    }
}