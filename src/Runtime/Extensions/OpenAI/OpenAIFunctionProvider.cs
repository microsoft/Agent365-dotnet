// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Runtime.Extensions.OpenAI
{
    /// <summary>
    /// Provides caching and management of OpenAI functions/tools per tenant and worker,
    /// using delegate-based approach for flexible function registration and execution.
    /// </summary>
    public class OpenAIFunctionProvider : IOpenAIFunctionProvider, IDisposable
    {
        private readonly ConcurrentDictionary<(string, string), (List<ChatTool> tools, Dictionary<string, Func<JsonNode?, Task<string>>> executors, DateTime lastUsed)> _functionCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
        private readonly Timer _evictionTimer;
        private bool _disposed;
        private readonly Func<string, string, (List<ChatTool> tools, Dictionary<string, Func<JsonNode?, Task<string>>> executors)> _configureFunctions;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAIFunctionProvider"/> class.
        /// </summary>
        /// <param name="configureFunctions">Delegate to configure functions for specific tenant/worker combinations.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public OpenAIFunctionProvider(
            Func<string, string, (List<ChatTool> tools, Dictionary<string, Func<JsonNode?, Task<string>>> executors)> configureFunctions,
            ILogger<OpenAIFunctionProvider>? logger = null)
        {
            _configureFunctions = configureFunctions ?? throw new ArgumentNullException(nameof(configureFunctions));
            _logger = logger;
            _evictionTimer = new Timer(EvictExpiredFunctions, null, _cacheExpiry, _cacheExpiry);
        }

        /// <inheritdoc/>
        public List<ChatTool> GetAvailableTools(string tenantId, string workerId)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(workerId);

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID cannot be empty or whitespace.", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker ID cannot be empty or whitespace.", nameof(workerId));

            var (tools, _, _) = GetOrCreateFunctionCache(tenantId, workerId);
            return tools;
        }

        /// <inheritdoc/>
        public async Task<string> ExecuteFunctionAsync(string functionName, string tenantId, string workerId, JsonNode? arguments = null)
        {
            ArgumentNullException.ThrowIfNull(functionName);
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(workerId);

            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function name cannot be empty or whitespace.", nameof(functionName));

            var (_, executors, _) = GetOrCreateFunctionCache(tenantId, workerId);

            if (executors.TryGetValue(functionName, out var executor))
            {
                _logger?.LogDebug("Executing function '{functionName}' for Tenant='{tenant}', Worker='{worker}'", functionName, tenantId, workerId);
                return await executor(arguments);
            }

            var errorMessage = $"Unknown function: {functionName}";
            _logger?.LogWarning("Function execution failed: {error} for Tenant='{tenant}', Worker='{worker}'", errorMessage, tenantId, workerId);
            return errorMessage;
        }

        private (List<ChatTool> tools, Dictionary<string, Func<JsonNode?, Task<string>>> executors, DateTime lastUsed) GetOrCreateFunctionCache(string tenantId, string workerId)
        {
            var key = (tenantId, workerId);
            var now = DateTime.UtcNow;

            // Check if we have cached functions
            if (_functionCache.TryGetValue(key, out var cachedEntry))
            {
                // Cache hit - update last used and return cached functions
                _functionCache[key] = (cachedEntry.tools, cachedEntry.executors, now);
                _logger?.LogDebug("Function cache hit for Tenant='{tenant}', Worker='{worker}', Tools={toolCount}", 
                    tenantId, workerId, cachedEntry.tools.Count);
                return (cachedEntry.tools, cachedEntry.executors, cachedEntry.lastUsed);
            }

            _logger?.LogInformation("Function cache miss for Tenant='{tenant}', Worker='{worker}'; creating new function set.", tenantId, workerId);

            // Cache miss - create new function set using delegate
            var (tools, executors) = _configureFunctions(tenantId, workerId);
            var newEntry = (tools, executors, now);
            _functionCache[key] = newEntry;
            
            _logger?.LogInformation("Created {toolCount} tools for Tenant='{tenant}', Worker='{worker}'", tools.Count, tenantId, workerId);
            return newEntry;
        }

        private void EvictExpiredFunctions(object? state)
        {
            var now = DateTime.UtcNow;
            var evicted = 0;
            foreach (var kvp in _functionCache)
            {
                if (now - kvp.Value.lastUsed > _cacheExpiry)
                {
                    if (_functionCache.TryRemove(kvp.Key, out var removed))
                    {
                        evicted++;
                    }
                }
            }

            if (evicted > 0)
            {
                _logger?.LogInformation("Evicted {count} expired function cache(s).", evicted);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            
            _evictionTimer.Dispose();
            _functionCache.Clear();
            _disposed = true;
        }
    }
}