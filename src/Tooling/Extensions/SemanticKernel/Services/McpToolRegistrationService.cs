// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Runtime.Authentication;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
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
        private readonly IMcpToolEnumerationService _mcpToolEnumerationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMcpToolRegistrationService"/> class.
        /// </summary>
        /// <param name="logger">
        /// Logger instance for logging.
        /// </param>
        /// <param name="serviceProvider">
        /// Service provider.
        /// </param>
        /// <param name="mcpToolEnumerationService">
        /// MCP tool enumeration service.
        /// </param>
        public McpToolRegistrationService(ILogger<IMcpToolRegistrationService> logger, IServiceProvider serviceProvider, IMcpToolEnumerationService mcpToolEnumerationService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpToolEnumerationService = mcpToolEnumerationService;
        }

        /// <inheritdoc />
        public async Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            authToken = await _mcpToolEnumerationService.GetAuthTokenAsync(userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);

            // resolve agent identity from context or token.
            string agenticAppId = RuntimeUtility.ResolveAgentIdentity(turnContext, authToken);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            var (servers, toolsByServer) = await _mcpToolEnumerationService.EnumerateToolsFromServersAsync(agenticAppId, authToken, turnContext, toolOptions).ConfigureAwait(false);

            foreach (var serverEntry in toolsByServer)
            {
                var pluginName = serverEntry.Key;
                var listAvailableToolsForServer = serverEntry.Value;

                // Tool names can only be 64 characters long, so filter out any that are too long. A tool name is the combination of the server name and tool name.
                listAvailableToolsForServer = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                kernel.Plugins.AddFromFunctions(pluginName, listAvailableToolsForServer.Select(x => x.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
        }
    }
}
