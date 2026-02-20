// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Tooling.Services
{
    /// <summary>
    /// Default implementation of <see cref="IMcpPolicyEnforcer"/> that checks server configuration
    /// for device-path requirements before allowing direct cloud access.
    /// </summary>
    public class McpPolicyEnforcer : IMcpPolicyEnforcer
    {
        private readonly IMcpToolServerConfigurationService _configService;
        private readonly ILogger<McpPolicyEnforcer> _logger;

        /// <summary>
        /// Cache of server configurations to avoid repeated lookups.
        /// Key is the server name (case-insensitive).
        /// </summary>
        private readonly Dictionary<string, Models.MCPServerConfig?> _serverConfigCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPolicyEnforcer"/> class.
        /// </summary>
        /// <param name="configService">MCP server configuration service.</param>
        /// <param name="logger">Logger instance.</param>
        public McpPolicyEnforcer(
            IMcpToolServerConfigurationService configService,
            ILogger<McpPolicyEnforcer> logger)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<McpPolicyEnforcementResult> EnforceAsync(
            string serverName,
            string toolName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serverName))
            {
                return McpPolicyEnforcementResult.Allowed();
            }

            _logger.LogDebug("Evaluating policy for {ServerName}/{ToolName}", serverName, toolName);

            try
            {
                // Check if this server is known (cached) — all known servers require policy enforcement
                var serverConfig = await GetServerConfigAsync(serverName, cancellationToken);

                if (serverConfig != null)
                {
                    _logger.LogWarning(
                        "Direct cloud access blocked for {ServerName}/{ToolName} - policy enforcement required",
                        serverName, toolName);

                    return McpPolicyEnforcementResult.DevicePathRequired(
                        serverName,
                        $"policy-enforcement-{serverName}");
                }

                _logger.LogDebug("Server {ServerName} not in policy cache, allowing {ToolName}", serverName, toolName);
                return McpPolicyEnforcementResult.Allowed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating policy for {ServerName}/{ToolName}", serverName, toolName);
                // On error, allow the call to proceed (fail-open)
                // This is a deliberate choice for POC; production might want fail-closed
                return McpPolicyEnforcementResult.Allowed();
            }
        }

        /// <summary>
        /// Gets server configuration from cache or loads it from the configuration service.
        /// </summary>
        private async Task<Models.MCPServerConfig?> GetServerConfigAsync(
            string serverName,
            CancellationToken cancellationToken)
        {
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_serverConfigCache.TryGetValue(serverName, out var cachedConfig))
                {
                    return cachedConfig;
                }

                // Server not in cache - we would need to load from config service
                // For now, return null and let the caller handle unknown servers
                // In production, this would query ATG or local config
                _logger.LogDebug("Server {ServerName} not found in policy cache", serverName);
                return null;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Preloads server configurations into the cache for efficient policy lookups.
        /// This should be called after listing tool servers.
        /// </summary>
        /// <param name="servers">The list of MCP server configurations.</param>
        public async Task PreloadServerConfigsAsync(IEnumerable<Models.MCPServerConfig> servers)
        {
            await _cacheLock.WaitAsync();
            try
            {
                foreach (var server in servers)
                {
                    _serverConfigCache[server.mcpServerName] = server;
                    _logger.LogDebug(
                        "Cached server {ServerName} for policy enforcement",
                        server.mcpServerName);
                }
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Clears the server configuration cache.
        /// </summary>
        public async Task ClearCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                _serverConfigCache.Clear();
            }
            finally
            {
                _cacheLock.Release();
            }
        }
    }
}
