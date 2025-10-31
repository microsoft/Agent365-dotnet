// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Services;

using Azure.AI.Agents;
using Azure.AI.Agents.Persistent; // MCPToolDefinition, MCPToolResource, MCPApproval
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Agents.A365.Runtime.Authentication;
using Microsoft.Agents.A365.Tooling.Extensions.AzureFoundry.Handlers;
using Microsoft.Agents.A365.Tooling;
using Microsoft.Agents.A365.Tooling.Models;
using Microsoft.Agents.A365.Tooling.Services;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Constants = Utils.Constants;
using Utility = Utils.Utility;

/// <summary>
/// Service for registering and validating MCP tool servers for Foundry (Persistent Agents) scenarios.
/// NOTE: Persistent Agents cannot be mutated to add tools after creation. This method therefore
/// performs discovery + validation and returns the MCP tool definitions and resources that a caller
/// SHOULD use when creating (or recreating) a PersistentAgent. No agent mutation is attempted here.
/// </summary>
public class McpToolRegistrationService : IMcpToolRegistrationService
{
    private readonly ILogger<IMcpToolRegistrationService> _logger;
    private readonly IServiceProvider _serviceProvider; // reserved for future DI expansion
    private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpToolRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="mcpToolServerConfigurationService">The MCP tool server configuration service.</param>
    public McpToolRegistrationService(
        ILogger<IMcpToolRegistrationService> logger,
        IServiceProvider serviceProvider,
        IMcpToolServerConfigurationService mcpToolServerConfigurationService)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _mcpServerConfigurationService = mcpToolServerConfigurationService;
    }

    /// <inheritdoc />
    public void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        string agentInstanceId,
        string environmentId,
        string? authToken = null)
    {
        if (agentClient == null)
        {
            throw new ArgumentNullException(nameof(agentClient));
        }

        try
        {
            // Get the tool definitions and resources using the internal implementation
            var (toolDefinitions, toolResources) = GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId, environmentId, authToken ?? string.Empty).GetAwaiter().GetResult();

            agentClient.Administration.UpdateAgent(
                agentInstanceId,
                tools: toolDefinitions,
                toolResources: toolResources);

            _logger.LogInformation("Successfully configured {Count} MCP tool servers for agent", toolDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure during MCP tool registration workflow for agent user {agentInstanceId}", agentInstanceId);
            throw;
        }
    }

    /// <inheritdoc />
    public void AddToolServersToAgent(
        PersistentAgentsClient agentClient,
        string agentInstanceId,
        string environmentId,
        UserAuthorization userAuthorization,
        ITurnContext turnContext,
        string? authToken = null)
    {
        if (agentClient == null)
        {
            throw new ArgumentNullException(nameof(agentClient));
        }

        // Acquire token if not explicitly supplied
        if (authToken == null)
        {
            authToken = AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, turnContext).Result;
        }

        try
        {
            // Perform the (potentially async) work in a dedicated task to keep this synchronous signature.
            var (toolDefinitions, toolResources) = GetMcpToolDefinitionsAndResourcesAsync(agentInstanceId, environmentId, authToken ?? string.Empty).GetAwaiter().GetResult();

            agentClient.Administration.UpdateAgent(
                agentInstanceId,
                tools: toolDefinitions,
                toolResources: toolResources);

            _logger.LogInformation("Successfully configured {Count} MCP tool servers for agent", toolDefinitions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled failure during MCP tool registration workflow for agent user {agentInstanceId}", agentInstanceId);
            throw;
        }
    }

    /// <summary>
    /// Get MCP tool definitions and resources.
    /// </summary>
    public async Task<(IList<MCPToolDefinition> ToolDefinitions, ToolResources? ToolResources)> GetMcpToolDefinitionsAndResourcesAsync(
        string agentInstanceId,
        string environmentId,
        string authToken)
    {
        // TODO: Make this method private 
        // Tool resources should ideally be accessible via agentClient after AddToolServersToAgent.
        // This workaround is temporary and will be removed once the Foundry SDK correctly updates agentClient with tool resources.
        // Eventually, we should retrieve tool resources directly from agentClient.

        var toolsMode = Utility.GetToolsMode();

        List<MCPServerConfig> servers;
        try
        {
            servers = await _mcpServerConfigurationService.ListToolServers(agentInstanceId, environmentId, authToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list MCP tool servers for AgentInstanceId={agentInstanceId}", agentInstanceId);
            return (new List<MCPToolDefinition>(), null);
        }

        if (servers.Count == 0)
        {
            _logger.LogInformation("No MCP servers configured for agentInstanceId={agentInstanceId}, EnvironmentId={EnvId}", agentInstanceId, environmentId);
            return (new List<MCPToolDefinition>(), null);
        }

        // Collections we build for the return value
        var toolDefinitions = new List<MCPToolDefinition>();
        var discoveredTools = new Dictionary<string, IList<McpClientTool>>(StringComparer.OrdinalIgnoreCase);

        // Initialize combined tool resources early
        ToolResources? combinedToolResources = new ToolResources();

        foreach (var server in servers)
        {
            // Defensive validation of config object
            if (string.IsNullOrWhiteSpace(server.mcpServerName) || string.IsNullOrWhiteSpace(server.url))
            {
                _logger.LogWarning("Skipping invalid MCP server config: Name='{Name}', Url='{Url}'", server.mcpServerName, server.url);
                continue;
            }

            // TODO: to remove the "mcp_" prefix handling, after the fix from foundry team is rolled out.
            var server_label = server.mcpServerName.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase)
                ? server.mcpServerName.Substring(4)
                : server.mcpServerName;

            // Build MCP tool artifacts (definition + resource w/headers) - this is the logic moved from MyAgent.cs
            var toolDef = new MCPToolDefinition(server_label, server.url);
            toolDefinitions.Add(toolDef);

            var resource = new MCPToolResource(server_label);

            // Set up authorization header
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                // Normalize bearer header (ensure it starts with "Bearer ")
                var headerValue = authToken.StartsWith($"{Constants.Headers.BearerPrefix} ", StringComparison.OrdinalIgnoreCase) ? authToken : $"{Constants.Headers.BearerPrefix} {authToken}";
                resource.UpdateHeader("Authorization", headerValue);
            }

            if (Utility.UseEnvironmentId())
            {
                // Set environment ID header
                resource.UpdateHeader(Constants.Headers.EnvironmentId, environmentId);
            }

            // Set approval requirement
            resource.RequireApproval = new MCPApproval("never");

            // Add directly to combined tool resources
            combinedToolResources.Mcp.Add(resource);

            if (toolsMode == ToolsMode.MockMCPServer)
            {
                continue; // Skip live validation in hardcoded mode
            }

            // Attempt live validation by connecting and listing tools; not used for updating the agentClient directly
            try
            {
                var mcpTools = await GetTools(server, environmentId, authToken);
                discoveredTools[server.mcpServerName] = mcpTools;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed for MCP server {Server} at {Url}", server.mcpServerName, server.url);
            }
        }

        // Return null if no servers were processed successfully
        if (combinedToolResources.Mcp.Count == 0)
        {
            combinedToolResources = null;
        }

        // Summarize for caller - keep important summary logs
        _logger.LogInformation("MCP server discovery summary: Servers={ServerCount}, ToolDefinitions={ToolDefCount}", servers.Count, toolDefinitions.Count);
        foreach (var kvp in discoveredTools)
        {
            _logger.LogInformation(" - {Server}: {ToolCount} MCP tools", kvp.Key, kvp.Value.Count);
        }
        _logger.LogInformation("MCP tool definitions and resources prepared: Servers={ServerCount}, ToolDefinitions={ToolDefCount}, ResourceCount={ResourceCount}",
            servers.Count, toolDefinitions.Count, combinedToolResources?.Mcp.Count ?? 0);

        return (toolDefinitions, combinedToolResources);
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
        if (Utility.UseEnvironmentId())
        {
            httpClient.DefaultRequestHeaders.Add(Constants.Headers.EnvironmentId, environmentId);
        }

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