// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using System;
    using System.Linq;

    /// <summary>
    /// Provides services related to tools in the Semantic Kernel.
    /// </summary>
    public class McpToolRegistrationService : IMcpToolRegistrationService
    {
        private readonly ILogger<IMcpToolRegistrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
        private readonly IConfiguration _configuration;

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
        /// <param name="configuration">Configuration Service for the application</param>
        public McpToolRegistrationService(ILogger<IMcpToolRegistrationService> logger, IServiceProvider serviceProvider, IMcpToolServerConfigurationService mcpServerConfigurationService, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpServerConfigurationService = mcpServerConfigurationService;
            _configuration = configuration;
        }

        /// <inheritdoc />
        public async Task AddToolServersToAgentAsync(Kernel kernel, string environmentId, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }

            string agenticAppId = "";
            // App ID is required to pass to MCP server URL. 
            if (turnContext.Activity.IsAgenticRequest())
            {
                agenticAppId = turnContext.Activity.GetAgenticInstanceId();
            }
            else
            {
                // we need to get the broker app ID from the authToken here. 
                agenticAppId = Runtime.Utils.Utility.GetAppIdFromToken(authToken);

            }
            var servers = await _mcpServerConfigurationService.ListToolServersAsync(agenticAppId, environmentId, authToken).ConfigureAwait(false);

            var toolsMode = Utility.GetToolsMode(_configuration);
            foreach (var server in servers)
            {
                var pluginName = $"{server.mcpServerName}";
                var listAvailableToolsForServer = await _mcpServerConfigurationService.GetMcpClientToolsAsync(turnContext, server, environmentId, authToken).ConfigureAwait(false);
                // Tool names can only be 64 characters long, so filter out any that are too long. A tool name is the combination of the server name and tool name.
                listAvailableToolsForServer = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                kernel.Plugins.AddFromFunctions(pluginName, listAvailableToolsForServer.Select(x => x.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
        }
    }
}
