// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using System;
    using System.Linq;
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;

    /// <summary>
    /// Provides services related to tools in the Semantic Kernel.
    /// </summary>
    public class McpToolRegistrationService : IMcpToolRegistrationService
    {
        private readonly ILogger<IMcpToolRegistrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
        private readonly IConfiguration _configuration;
        private readonly IMcpToolEnumerationService _mcpToolEnumerationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMcpToolRegistrationService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for logging.</param>
        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="mcpServerConfigurationService">MCP server configuration service.</param>
        /// <param name="mcpToolEnumerationService">MCP tool enumeration service.</param>
        /// <param name="configuration">Configuration Service for the application.</param>
        public McpToolRegistrationService(
            ILogger<IMcpToolRegistrationService> logger,
            IServiceProvider serviceProvider,
            IMcpToolServerConfigurationService mcpServerConfigurationService,
            IMcpToolEnumerationService mcpToolEnumerationService,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpServerConfigurationService = mcpServerConfigurationService;
            _mcpToolEnumerationService = mcpToolEnumerationService;
            _configuration = configuration;
        }

        /// <inheritdoc />
        public async Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }

            // resolve agent identity from context or token.
            string agenticAppId = RuntimeUtility.ResolveAgentIdentity(turnContext, authToken);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            var (_, toolsByServer) = await _mcpToolEnumerationService.EnumerateToolsFromServersAsync(agenticAppId, authToken, turnContext, toolOptions).ConfigureAwait(false);

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

        /// <inheritdoc />
        public async Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(chatHistory);
            cancellationToken.ThrowIfCancellationRequested();

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            return await SendChatHistoryAsync(turnContext, chatHistory, toolOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<OperationResult> SendChatHistoryAsync(ITurnContext turnContext, ChatHistory chatHistory, ToolOptions toolOptions, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(chatHistory);
            ArgumentNullException.ThrowIfNull(toolOptions);
            cancellationToken.ThrowIfCancellationRequested();

            // Convert ChatHistory to ChatHistoryMessage[]
            // Note: ChatHistory does not include timestamps, so all messages are timestamped with the current UTC time
            var chatHistoryMessages = chatHistory.Select(message => new ChatHistoryMessage(
                id: Guid.NewGuid().ToString(),
                role: message.Role.Label,
                content: message.Content ?? string.Empty,
                timestamp: DateTimeOffset.UtcNow
            )).ToArray();

            return await _mcpServerConfigurationService.SendChatHistoryAsync(turnContext, chatHistoryMessages, toolOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}
