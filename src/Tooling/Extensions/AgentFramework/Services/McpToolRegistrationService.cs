// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System.Net.Http;
using Microsoft.Agents.A365.Tooling.Services;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Utils;
using Microsoft.Agents.A365.Tooling.Extensions.AgentFramework.Handlers;

/// <summary>
/// Service for registering and validating MCP tool servers for Agent Framework scenarios.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="mcpServerConfigurationService">The MCP tool server configuration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IMcpToolServerConfigurationService mcpServerConfigurationService)
    {
        _logger = logger;
        _mcpServerConfigurationService = mcpServerConfigurationService;
    }

    /// <inheritdoc />
    /// <summary>
    /// Add new MCP servers to the agent by creating a new Agent instance.
    /// 
    /// Note: Due to Microsoft.Extensions.AI framework limitations, MCP tools must be set during
    /// Agent creation. If new tools are found, this method creates a new Agent
    /// instance with all tools (existing + new) properly initialized.
    /// </summary>
    /// <param name="chatClient">The configured IChatClient to use for creating the agent</param>
    /// <param name="agentInstructions">The agent instructions</param>
    /// <param name="initialTools">The existing tools to add servers to</param>
    /// <param name="agentUserId">Agent User ID for the agent</param>
    /// <param name="environmentId">Environment ID for the environment</param>
    /// <param name="authToken">Authentication token to access the MCP servers</param>
    /// <returns>New Agent instance with all MCP tools, or agent with original tools if no new servers</returns>
    public async Task<AIAgent> AddToolServersToAgent(
        IChatClient chatClient,
        string agentInstructions,
        IList<AITool> initialTools,
        string agentUserId,
        string environmentId,
        string? authToken = null)
    {
        if (chatClient == null)
        {
            throw new ArgumentNullException(nameof(chatClient));
        }

        if (string.IsNullOrWhiteSpace(authToken))
        {
            throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));
        }
        
        try
        {
            // Step 2: Now update agent by adding MCP tools
            var updatedTools = new List<AITool>();
            
            // Keep any existing tools that were passed in
            updatedTools.AddRange(initialTools);

            // Get MCP tool server configurations
            var servers = await _mcpServerConfigurationService.ListToolServers(agentUserId, environmentId, authToken!);
            
            // Retrieve MCP tools from all configured servers
            foreach (var server in servers)
            {
                try
                {
                    var mcpTools = await GetTools(server, environmentId, authToken);
                    // Add the MCP tools
                    updatedTools.AddRange(mcpTools.Cast<AITool>());
                    
                    _logger.LogInformation("Successfully loaded {ToolCount} tools from MCP server '{ServerName}'", 
                        mcpTools.Count, server.mcpServerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tools from MCP server '{ServerName}': {Error}", 
                        server.mcpServerName, ex.Message);
                }
            }

            _logger.LogInformation("Loaded {McpCount} MCP tools for agent {AgentUserId} in environment {EnvironmentId}",
                updatedTools.Count, agentUserId, environmentId);

            // Create agent with updated tools (since AIAgent is immutable)
            var agentWithTools = chatClient.CreateAIAgent(
                instructions: agentInstructions,
                tools: [.. updatedTools]);

            // Return the enhanced agent
            return agentWithTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MCP tool servers for agent {AgentUserId} in environment {EnvironmentId}", 
                agentUserId, environmentId);
            throw;
        }
    }

    private async Task<IList<McpClientTool>> GetTools(MCPServerConfig mCPServerConfig, string environmentId, string authToken)
    {
        try
        {
            // Validate the server name
            if (string.IsNullOrWhiteSpace(mCPServerConfig.mcpServerName))
            {
                throw new ArgumentException("MCP Server name cannot be null or empty", nameof(mCPServerConfig.mcpServerName));
            }

            // Use custom HTTP-based implementation since MCP client library doesn't work
            var mcpClient = await CreateMcpClientWithAuthHandlers(new Uri(mCPServerConfig.url), mCPServerConfig.mcpServerName, environmentId, authToken);
            var tools = await mcpClient.ListToolsAsync();

            return tools;
        }
        catch (HttpRequestException httpEx)
        {
            throw new InvalidOperationException($"HTTP error connecting to MCP server '{mCPServerConfig.mcpServerName}' at '{mCPServerConfig.url}': {httpEx.Message}", httpEx);
        }
        catch (ArgumentException argEx)
        {
            throw new InvalidOperationException($"Invalid configuration for MCP server '{mCPServerConfig.mcpServerName}': {argEx.Message}", argEx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get tools from MCP server '{mCPServerConfig.mcpServerName}' at '{mCPServerConfig.url}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates an MCP client with authentication handlers similar to your reference implementation
    /// </summary>
    private async Task<IMcpClient> CreateMcpClientWithAuthHandlers(Uri endpoint, string clientName, string environmentId, string authToken)
    {
        // Create HTTP client handler chain for MCP service authentication
        var httpClientHandler = new HttpClientHandler();

        // WARNING: Only use this in development/testing - never in production!
        // This bypasses SSL certificate validation
        var isDevScenario = IsDevScenario();
        if (isDevScenario)
        {
            httpClientHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        // Create a simple authentication handler that adds the bearer token
        var authHandler = new BearerTokenHandler(authToken)
        {
            InnerHandler = httpClientHandler
        };

        // Create logging handler (optional - for debugging HTTP requests)
        var loggingHandler = new HttpLoggingHandler(this._logger)
        {
            InnerHandler = authHandler
        };

        // Setup SSE client transport options without manual token management
        var options = new SseClientTransportOptions
        {
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.AutoDetect,
        };

        // Create HTTP client with the authentication handler chain
        var httpClient = new HttpClient(loggingHandler);
        httpClient.DefaultRequestHeaders.Add(Constants.Headers.EnvironmentId, environmentId);

        var clientTransport = new SseClientTransport(options, httpClient);

        try
        {
            return await McpClientFactory.CreateAsync(clientTransport);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create MCP client for endpoint '{endpoint}': {ex.Message}", ex);
        }
    }

    private static bool IsDevScenario()
    {
        // Check environment variable first, default to dev if not set
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                         Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                         "Development";

        return environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
    }
}