// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;

    /// <summary>
    /// Provides services related to tools in the Semantic Kernel.
    /// </summary>
    public class McpToolRegistrationService : IMcpToolRegistrationService
    {
        // MCP plugins created by this service. Each call removes them from the kernel before adding its own.
        private static readonly ConditionalWeakTable<KernelPlugin, object> s_mcpPlugins = new();
        private static readonly object s_marker = new();

        // The tool set whose MCP clients serve the plugins that the last call added to a kernel. Only that kernel owns it:
        // Kernel.Clone() copies share the plugins but not the tool set, so removing the plugins from a copy doesn't dispose it.
        private static readonly ConditionalWeakTable<Kernel, McpToolSet> s_toolSets = new();

        // Kernels that an AddToolServersToAgentAsync or RemoveToolServersAsync call is in progress for.
        private static readonly ConditionalWeakTable<Kernel, object> s_kernelsInUse = new();

        private readonly ILogger<IMcpToolRegistrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="IMcpToolRegistrationService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for logging.</param>
        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="mcpServerConfigurationService">MCP server configuration service.</param>
        /// <param name="configuration">Configuration Service for the application.</param>
        public McpToolRegistrationService(
            ILogger<IMcpToolRegistrationService> logger,
            IServiceProvider serviceProvider,
            IMcpToolServerConfigurationService mcpServerConfigurationService,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpServerConfigurationService = mcpServerConfigurationService;
            _configuration = configuration;
        }

        /// <inheritdoc />
        public async Task AddToolServersToAgentAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken = null)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            EnterKernel(kernel);
            try
            {
                await AddToolServersCoreAsync(kernel, userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);
            }
            finally
            {
                s_kernelsInUse.Remove(kernel);
            }
        }

        private async Task AddToolServersCoreAsync(Kernel kernel, UserAuthorization userAuthorization, string authHandlerName, ITurnContext turnContext, string? authToken)
        {
            // Remove plugins added by an earlier call (also present in Kernel.Clone() copies) before this call adds its own,
            // and dispose the MCP clients that serve them if this kernel owns them.
            await RemoveMcpPluginsAsync(kernel).ConfigureAwait(false);

            // Try to resolve the auth token for the tool discovery service directly, which will handle both V1 and V2 auth scenarios
            // If this code is used outside of the Agent SDK context, the auth token may need to be provided directly, so we fall back to that if resolution fails.
            if (authToken is null && userAuthorization is not null && authHandlerName is not null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }
            authToken ??= string.Empty;

            // resolve agent identity from context or token.
            string agenticAppId = RuntimeUtility.ResolveAgentIdentity(turnContext, authToken);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            IMcpTokenProvider tokenProvider =
              (userAuthorization is not null && authHandlerName is not null)
                  ? new TokenProviderCollection(_logger,
                     new EnvMcpTokenProvider(_configuration, _logger),
                     new AgenticMcpTokenProvider(userAuthorization, authHandlerName, turnContext, _configuration, _logger))
                  :
                     new TokenProviderCollection(_logger, new EnvMcpTokenProvider(_configuration, _logger));

            var toolSet = await _mcpServerConfigurationService.GetMcpToolSetAsync(agenticAppId, authToken, tokenProvider, turnContext, toolOptions).ConfigureAwait(false);

            try
            {
                foreach (var serverEntry in toolSet.ToolsByServer)
                {
                    var pluginName = serverEntry.Key;

                    // Tool names can only be 64 characters long, so filter out any that are too long. A tool name is the combination of the server name and tool name.
                    var listAvailableToolsForServer = serverEntry.Value.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    var plugin = kernel.Plugins.AddFromFunctions(pluginName, listAvailableToolsForServer.Select(x => x.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    s_mcpPlugins.AddOrUpdate(plugin, s_marker);
                }
            }
            catch
            {
                // Don't leave the plugins of a partly registered call, or their MCP clients, behind.
                RemoveMcpPlugins(kernel);
                await toolSet.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            s_toolSets.AddOrUpdate(kernel, toolSet);
        }

        /// <inheritdoc />
        public Task RemoveToolServersAsync(Kernel kernel) => RemoveToolServersFromKernelAsync(kernel);

        /// <summary>
        /// Removes the MCP plugins that <see cref="AddToolServersToAgentAsync"/> added to the kernel, and disposes the MCP clients
        /// that serve them if the kernel owns them.
        /// </summary>
        internal static async Task RemoveToolServersFromKernelAsync(Kernel kernel)
        {
            ArgumentNullException.ThrowIfNull(kernel);

            EnterKernel(kernel);
            try
            {
                await RemoveMcpPluginsAsync(kernel).ConfigureAwait(false);
            }
            finally
            {
                s_kernelsInUse.Remove(kernel);
            }
        }

        private static async Task RemoveMcpPluginsAsync(Kernel kernel)
        {
            RemoveMcpPlugins(kernel);

            if (s_toolSets.TryGetValue(kernel, out var toolSet))
            {
                s_toolSets.Remove(kernel);
                await toolSet.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static void RemoveMcpPlugins(Kernel kernel)
        {
            foreach (var plugin in kernel.Plugins.Where(p => s_mcpPlugins.TryGetValue(p, out _)).ToList())
            {
                kernel.Plugins.Remove(plugin);
            }
        }

        private static void EnterKernel(Kernel kernel)
        {
            if (!s_kernelsInUse.TryAdd(kernel, s_marker))
            {
                throw new InvalidOperationException(
                    "An AddToolServersToAgentAsync or RemoveToolServersAsync call for this kernel is already in progress. " +
                    "Turns that run concurrently must each use their own kernel, for example a Kernel.Clone() copy of a base kernel.");
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
