// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Logging;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Handlers;
    using Microsoft.SemanticKernel;
    using ModelContextProtocol.Client;

    /// <summary>
    /// Provides services related to tools in the Semantic Kernel.
    /// </summary>
    public class McpToolRegistrationService : IMcpToolRegistrationService
    {
        private readonly ILogger<IMcpToolRegistrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMcpToolRegistrationService"/> class.
        /// </summary>
        /// <param name="logger">
        /// Logger instance for logging.
        /// </param>
        /// <param name="serviceProvider">
        /// Service provider.
        /// </param>
        /// <param name="mcpServerConfigurationService">
        /// MCP server configuration service.
        /// </param>
        public McpToolRegistrationService(ILogger<IMcpToolRegistrationService> logger, IServiceProvider serviceProvider, IMcpToolServerConfigurationService mcpServerConfigurationService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpServerConfigurationService = mcpServerConfigurationService;
        }

        /// <summary>
        /// Adds the A365 MCP Tool Servers
        /// </summary>
        /// <param name="kernel">Kernel</param>
        /// <param name="agentInstanceId">Agent Instance Id for the agent.</param>
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="userAuthorization"></param>
        /// <param name="turnContext"></param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns a new object of the kernel</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddToolServersToAgent(Kernel kernel, string agentInstanceId, string environmentId, UserAuthorization userAuthorization, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, turnContext).Result;
            }

            var servers = _mcpServerConfigurationService.ListToolServers(agentInstanceId, environmentId, authToken).Result;

            var toolsMode = Utility.GetToolsMode();
            foreach (var server in servers)
            {
                var pluginName = $"{server.mcpServerName}";
                var listAvailableToolsForServer = GetTools(server, environmentId, authToken).Result;
                // Tool names can only be 64 characters long, so filter out any that are too long. A tool name is the combination of the server name and tool name.
                listAvailableToolsForServer = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                kernel.Plugins.AddFromFunctions(pluginName, listAvailableToolsForServer.Select(x => x.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
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

                this._logger.LogInformation($"Creating custom MCP client for: {mCPServerConfig.mcpServerName} at {mCPServerConfig.url}");

                // Use custom HTTP-based implementation since MCP client library doesn't work
                var mcpClient = await CreateMcpClientWithAuthHandlers(new Uri(mCPServerConfig.url), mCPServerConfig.mcpServerName, environmentId, authToken);
                var tools = await mcpClient.ListToolsAsync();

                this._logger.LogInformation($"Successfully retrieved {tools.Count} tools from {mCPServerConfig.mcpServerName}");

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
                this._logger.LogInformation("WARNING: SSL certificate validation disabled for development!");
            }

            // Create a simple authentication handler that adds the bearer token
            var authHandler = new BearerTokenHandler(authToken)
            {
                InnerHandler = httpClientHandler
            };

            this._logger.LogInformation($"Configured authentication handler for MCP endpoint {endpoint}");

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
}
