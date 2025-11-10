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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.A365.Tooling.Utils;
    using Microsoft.SemanticKernel;

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
        /// <param name="configuration">The application configuration.</param>
        /// <param name="environmentId">Environment Id for the environment</param>
        /// <param name="userAuthorization"></param>
        /// <param name="turnContext"></param>
        /// <param name="authToken">Auth token to access the MCP servers</param>
        /// <returns>Returns a new object of the kernel</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task AddToolServersToAgentAsync(Kernel kernel, IConfiguration configuration, string environmentId, UserAuthorization userAuthorization, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, turnContext, configuration).ConfigureAwait(false);
            }

            var agenticAppId = turnContext.Activity.Recipient.AgenticAppId;
            var servers = await _mcpServerConfigurationService.ListToolServersAsync(agenticAppId, environmentId, authToken).ConfigureAwait(false);

            var toolsMode = Utility.GetToolsMode();
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
