// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Collections.Concurrent;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Runtime.Extensions.OpenAI
{
    /// <summary>
    /// Provides caching and management of <see cref="ChatClient"/> instances per tenant and worker,
    /// using a delegate-based approach for flexible ChatClient creation.
    /// </summary>
    public class ChatClientProvider : IChatClientProvider, IDisposable
    {
        private readonly ConcurrentDictionary<(string, string), (ChatClient client, DateTime lastUsed)> _clientCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
        private readonly Timer _evictionTimer;
        private bool _disposed;
        private readonly Func<string, string, ChatClient> _createChatClient;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatClientProvider"/> class.
        /// </summary>
        /// <param name="createChatClient">Delegate to create ChatClient instances for specific tenant/worker combinations.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ChatClientProvider(Func<string, string, ChatClient> createChatClient, ILogger<ChatClientProvider>? logger = null)
        {
            _createChatClient = createChatClient ?? throw new ArgumentNullException(nameof(createChatClient));
            _logger = logger;
            _evictionTimer = new Timer(EvictExpiredClients, null, _cacheExpiry, _cacheExpiry);
        }

        /// <inheritdoc/>
        public ChatClient GetChatClient(string tenantId, string workerId)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(workerId);

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID cannot be empty or whitespace.", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker ID cannot be empty or whitespace.", nameof(workerId));

            var key = (tenantId, workerId);
            var now = DateTime.UtcNow;

            // Check if we have a cached client
            if (_clientCache.TryGetValue(key, out var cachedEntry))
            {
                // Cache hit - update last used and return cached client
                _clientCache[key] = (cachedEntry.client, now);
                _logger?.LogDebug("ChatClient cache hit for Tenant='{tenant}', Worker='{worker}'", tenantId, workerId);
                return cachedEntry.client;
            }

            _logger?.LogInformation("ChatClient cache miss for Tenant='{tenant}', Worker='{worker}'; creating new ChatClient.", tenantId, workerId);

            // Cache miss - create new client using delegate
            var client = _createChatClient(tenantId, workerId);
            _clientCache[key] = (client, now);
            return client;
        }

        private void EvictExpiredClients(object? state)
        {
            var now = DateTime.UtcNow;
            var evicted = 0;
            foreach (var kvp in _clientCache)
            {
                if (now - kvp.Value.lastUsed > _cacheExpiry)
                {
                    if (_clientCache.TryRemove(kvp.Key, out var removed))
                    {
                        evicted++;
                        // If client needs disposal, handle here
                        if (removed.client is IDisposable disposableClient)
                        {
                            disposableClient.Dispose();
                        }
                    }
                }
            }

            if (evicted > 0)
            {
                _logger?.LogInformation("Evicted {count} expired ChatClient(s) from cache.", evicted);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            
            _evictionTimer.Dispose();
            
            // Dispose all cached clients
            foreach (var (client, _) in _clientCache.Values)
            {
                if (client is IDisposable disposableClient)
                {
                    disposableClient.Dispose();
                }
            }
            
            _clientCache.Clear();
            _disposed = true;
        }
    }
}