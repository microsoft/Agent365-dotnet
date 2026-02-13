// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Microsoft.Agents.A365.Tooling.Extensions.SemanticKernel.Services
{
    using Microsoft.Agents.A365.Runtime;
    using Microsoft.Agents.A365.Runtime.Authentication;
    using RuntimeUtility = Microsoft.Agents.A365.Runtime.Utils.Utility;
    using Microsoft.Agents.A365.Tooling.Models;
    using Microsoft.Agents.A365.Tooling.Services;
    using Microsoft.Agents.Builder;
    using Microsoft.Agents.Builder.App.UserAuth;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;

    /// <summary>
    /// Provides services related to tools in the Semantic Kernel.
    /// </summary>
    public class McpToolRegistrationService : IMcpToolRegistrationService
    {
        private readonly ILogger<IMcpToolRegistrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMcpToolServerConfigurationService _mcpServerConfigurationService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILocalMcpScopeValidator? _scopeValidator;

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
        /// <param name="httpClientFactory">HTTP client factory for creating HTTP clients.</param>
        public McpToolRegistrationService(ILogger<IMcpToolRegistrationService> logger, IServiceProvider serviceProvider, IMcpToolServerConfigurationService mcpServerConfigurationService, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _mcpServerConfigurationService = mcpServerConfigurationService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            _scopeValidator = serviceProvider.GetService<ILocalMcpScopeValidator>();
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

            var servers = await _mcpServerConfigurationService.ListToolServersAsync(agenticAppId, authToken, toolOptions).ConfigureAwait(false);

            foreach (var server in servers)
            {
                // Sanitize plugin name: Semantic Kernel only allows ASCII letters, digits, and underscores
                var pluginName = SanitizePluginName(server.mcpServerName);
                _logger.LogInformation("Registering plugin '{PluginName}' (from server '{ServerName}')", pluginName, server.mcpServerName);

                var listAvailableToolsForServer = await _mcpServerConfigurationService.GetMcpClientToolsAsync(turnContext, server, authToken, toolOptions).ConfigureAwait(false);
                var originalCount = listAvailableToolsForServer.Count;

                // Tool names can only be 64 characters long, so filter out any that are too long. A tool name is the combination of the server name and tool name.
                listAvailableToolsForServer = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();

                if (listAvailableToolsForServer.Count < originalCount)
                {
                    _logger.LogWarning("Filtered out {FilteredCount} tools from '{PluginName}' because name length exceeded 64 characters (plugin name length: {PluginNameLength})",
                        originalCount - listAvailableToolsForServer.Count, pluginName, pluginName.Length);
                }

                _logger.LogInformation("Adding {ToolCount} tools to plugin '{PluginName}'", listAvailableToolsForServer.Count, pluginName);
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                kernel.Plugins.AddFromFunctions(pluginName, listAvailableToolsForServer.Select(x => x.AsKernelFunction()));
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
        }

        /// <summary>
        /// Maximum plugin name length to allow room for tool names within the 64-character limit.
        /// </summary>
        private const int MaxPluginNameLength = 30;

        /// <summary>
        /// Sanitizes a server name to be a valid Semantic Kernel plugin name.
        /// Plugin names can only contain ASCII letters, digits, and underscores.
        /// The result is truncated to MaxPluginNameLength to ensure tools fit within the 64-character limit.
        /// </summary>
        private static string SanitizePluginName(string serverName)
        {
            if (string.IsNullOrEmpty(serverName))
            {
                return "UnnamedPlugin";
            }

            // For very long server names (like Windows MCP server IDs), extract the meaningful part
            // e.g., "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"
            // should become "file_mcp_server" or similar
            var simplifiedName = ExtractMeaningfulName(serverName);

            // Replace dots, hyphens, and other invalid characters with underscores
            var sanitized = new System.Text.StringBuilder(simplifiedName.Length);
            foreach (var c in simplifiedName)
            {
                if (char.IsLetterOrDigit(c) && c < 128) // ASCII letters and digits
                {
                    sanitized.Append(c);
                }
                else if (c == '_')
                {
                    sanitized.Append(c);
                }
                else
                {
                    sanitized.Append('_');
                }
            }

            var result = sanitized.ToString();

            // Ensure it doesn't start with a digit
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            // Truncate if still too long
            if (result.Length > MaxPluginNameLength)
            {
                result = result.Substring(0, MaxPluginNameLength);
            }

            return string.IsNullOrEmpty(result) ? "UnnamedPlugin" : result;
        }

        /// <summary>
        /// Extracts a meaningful short name from a long server identifier.
        /// Handles Windows MCP server IDs like "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"
        /// </summary>
        private static string ExtractMeaningfulName(string serverName)
        {
            // For Windows MCP server IDs, the meaningful part is usually after the last underscore
            // e.g., "..._file-mcp-server" -> "file-mcp-server"
            if (serverName.Contains("mcpServer_", StringComparison.OrdinalIgnoreCase))
            {
                var lastUnderscoreIndex = serverName.LastIndexOf('_');
                if (lastUnderscoreIndex > 0 && lastUnderscoreIndex < serverName.Length - 1)
                {
                    return serverName.Substring(lastUnderscoreIndex + 1);
                }
            }

            // If it looks like a package family name with underscores, try to extract the last part
            var underscoreParts = serverName.Split('_');
            if (underscoreParts.Length > 1)
            {
                var lastPart = underscoreParts[underscoreParts.Length - 1];
                // If the last part is meaningful (not a hash or short ID), use it
                if (lastPart.Length >= 4 && !string.IsNullOrWhiteSpace(lastPart))
                {
                    return lastPart;
                }
            }

            // For names like "mcp_MailTools" or similar, return as-is
            return serverName;
        }

        /// <inheritdoc />
        public async Task AddToolServersWithLocalDiscoveryAsync(
            Kernel kernel,
            UserAuthorization userAuthorization,
            string authHandlerName,
            ITurnContext turnContext,
            string? localClientName,
            string? authToken = null,
            CancellationToken cancellationToken = default)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }

            // Resolve agent identity from context or token.
            string agenticAppId = RuntimeUtility.ResolveAgentIdentity(turnContext, authToken);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            // Use the new discovery method that includes local servers
            var servers = await _mcpServerConfigurationService.ListToolServersWithLocalDiscoveryAsync(
                agenticAppId, authToken, toolOptions, localClientName, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("[Discovery] Registering tools from {ServerCount} MCP servers (cloud + local)", servers.Count);

            foreach (var server in servers)
            {
                try
                {
                    // Sanitize plugin name: Semantic Kernel only allows ASCII letters, digits, and underscores
                    var pluginName = SanitizePluginName(server.mcpServerName);
                    _logger.LogInformation("Registering plugin '{PluginName}' (from server '{ServerName}', transport: {Transport}, hasStaticTools: {HasStatic})",
                        pluginName, server.mcpServerName, server.transportType, server.HasStaticTools);

                    IEnumerable<KernelFunction> kernelFunctions;

                    // For WNS servers with static tools, use lazy loading to avoid unnecessary MCP connections
                    if (server.HasStaticTools && server.transportType == McpTransportType.Wns)
                    {
                        _logger.LogInformation("[LazyMcp] Using static tools for '{ServerName}' - deferring MCP connection until tool invocation",
                            server.mcpServerName);

                        // Pass scope validator for local MCP servers to validate consent before tool invocation
                        var lazyWrapper = LazyMcpToolWrapper.GetOrCreate(server, _httpClientFactory, _loggerFactory, _scopeValidator);
                        kernelFunctions = lazyWrapper.CreateKernelFunctions();
                    }
                    else
                    {
                        // For cloud servers or servers without static tools, use the traditional flow
                        var listAvailableToolsForServer = await _mcpServerConfigurationService.GetMcpClientToolsAsync(
                            turnContext, server, authToken, toolOptions).ConfigureAwait(false);

                        // Tool names can only be 64 characters long, so filter out any that are too long.
                        var filteredTools = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();

                        if (filteredTools.Count < listAvailableToolsForServer.Count)
                        {
                            _logger.LogWarning("Filtered out {FilteredCount} tools from '{PluginName}' because name length exceeded 64 characters",
                                listAvailableToolsForServer.Count - filteredTools.Count, pluginName);
                        }

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
                        kernelFunctions = filteredTools.Select(x => x.AsKernelFunction());
#pragma warning restore SKEXP0001
                    }

                    var functionList = kernelFunctions.ToList();

                    // Filter out tools with names that are too long
                    functionList = functionList.Where(f => (f.Name.Length + pluginName.Length + 1) <= 64).ToList();

                    _logger.LogInformation("Adding {ToolCount} tools to plugin '{PluginName}': [{ToolNames}]", 
                        functionList.Count, pluginName, string.Join(", ", functionList.Select(f => f.Name)));
                    kernel.Plugins.AddFromFunctions(pluginName, functionList);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other servers
                    _logger.LogError(ex, "Failed to register tools from server '{ServerName}'", server.mcpServerName);
                }
            }
        }

        /// <inheritdoc />
        public async Task<LocalDiscoveryResult> AddToolServersWithUserDiscoveryAsync(
            Kernel kernel,
            UserAuthorization userAuthorization,
            string authHandlerName,
            ITurnContext turnContext,
            string? authToken = null,
            CancellationToken cancellationToken = default)
        {
            if (kernel == null)
            {
                throw new ArgumentNullException(nameof(kernel));
            }

            if (authToken == null)
            {
                authToken = await AgenticAuthenticationService.GetAgenticUserTokenAsync(userAuthorization, authHandlerName, turnContext, _configuration).ConfigureAwait(false);
            }

            // Get user identity from the turn context
            var userIdentifier = turnContext.Activity.From?.Name;
            if (string.IsNullOrWhiteSpace(userIdentifier))
            {
                _logger.LogWarning("[UserDiscovery] No user identifier found in turnContext.Activity.From.Name. Falling back to cloud-only discovery.");
                
                // Fall back to cloud-only discovery
                await AddToolServersToAgentAsync(kernel, userAuthorization, authHandlerName, turnContext, authToken).ConfigureAwait(false);
                return new LocalDiscoveryResult
                {
                    ErrorMessage = "No user identifier available. Only cloud tools loaded."
                };
            }

            // Resolve agent identity from context or token.
            string agenticAppId = RuntimeUtility.ResolveAgentIdentity(turnContext, authToken);

            var toolOptions = new ToolOptions
            {
                UserAgentConfiguration = Agent365SemanticKernelSdkUserAgentConfiguration.Instance
            };

            // Use the new user-based discovery method
            var discoveryResult = await _mcpServerConfigurationService.ListToolServersWithUserDiscoveryAsync(
                agenticAppId, authToken, toolOptions, userIdentifier, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("[UserDiscovery] Registering tools from {ServerCount} MCP servers. ActiveDesktop: {ActiveDesktop}, TotalDesktops: {TotalDesktops}", 
                discoveryResult.Servers.Count,
                discoveryResult.ActiveDesktop?.ClientName ?? "none",
                discoveryResult.AllRegisteredDesktops.Count);

            foreach (var server in discoveryResult.Servers)
            {
                try
                {
                    // Sanitize plugin name: Semantic Kernel only allows ASCII letters, digits, and underscores
                    var pluginName = SanitizePluginName(server.mcpServerName);
                    _logger.LogInformation("Registering plugin '{PluginName}' (from server '{ServerName}', transport: {Transport}, hasStaticTools: {HasStatic})",
                        pluginName, server.mcpServerName, server.transportType, server.HasStaticTools);

                    IEnumerable<KernelFunction> kernelFunctions;

                    // For WNS servers with static tools, use lazy loading to avoid unnecessary MCP connections
                    if (server.HasStaticTools && server.transportType == McpTransportType.Wns)
                    {
                        _logger.LogInformation("[LazyMcp] Using static tools for '{ServerName}' - deferring MCP connection until tool invocation",
                            server.mcpServerName);

                        var lazyWrapper = LazyMcpToolWrapper.GetOrCreate(server, _httpClientFactory, _loggerFactory, _scopeValidator);
                        kernelFunctions = lazyWrapper.CreateKernelFunctions();
                    }
                    else
                    {
                        var listAvailableToolsForServer = await _mcpServerConfigurationService.GetMcpClientToolsAsync(
                            turnContext, server, authToken, toolOptions).ConfigureAwait(false);

                        var filteredTools = listAvailableToolsForServer.Where(t => (t.Name.Length + pluginName.Length + 1) <= 64).ToList();

                        if (filteredTools.Count < listAvailableToolsForServer.Count)
                        {
                            _logger.LogWarning("Filtered out {FilteredCount} tools from '{PluginName}' because name length exceeded 64 characters",
                                listAvailableToolsForServer.Count - filteredTools.Count, pluginName);
                        }

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
                        kernelFunctions = filteredTools.Select(x => x.AsKernelFunction());
#pragma warning restore SKEXP0001
                    }

                    var functionList = kernelFunctions.ToList();
                    functionList = functionList.Where(f => (f.Name.Length + pluginName.Length + 1) <= 64).ToList();

                    _logger.LogInformation("Adding {ToolCount} tools to plugin '{PluginName}': [{ToolNames}]", 
                        functionList.Count, pluginName, string.Join(", ", functionList.Select(f => f.Name)));
                    kernel.Plugins.AddFromFunctions(pluginName, functionList);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register tools from server '{ServerName}'", server.mcpServerName);
                }
            }

            return discoveryResult;
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
