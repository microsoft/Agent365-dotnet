// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Caches observability tokens per (agentId, tenantId) with expiration and invalidation support.
    /// Includes automatic periodic cleanup of expired tokens for improved memory management.
    /// </summary>
    public class ServiceTokenCache : IExporterTokenCache<string>, IDisposable
    {
        private sealed class Entry
        {
            public string Token { get; }
            public string[] Scopes { get; }
            public DateTimeOffset ExpiresAt { get; }

            public Entry(string token, string[] scopes, DateTimeOffset expiresAt)
            {
                Token = token;
                Scopes = scopes;
                ExpiresAt = expiresAt;
            }
        }

        private readonly ConcurrentDictionary<string, Entry> _map = new ConcurrentDictionary<string, Entry>();
        private readonly TimeSpan _defaultExpiration;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Default interval for automatic cleanup of expired tokens (5 minutes).
        /// </summary>
        public static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceTokenCache"/> class.
        /// </summary>
        /// <param name="defaultExpiration">The default expiration time for tokens. Defaults to 1 hour if not specified.</param>
        /// <param name="cleanupInterval">The interval for automatic cleanup of expired tokens. Defaults to 5 minutes if not specified. Set to null to disable automatic cleanup.</param>
        public ServiceTokenCache(TimeSpan? defaultExpiration = null, TimeSpan? cleanupInterval = null)
        {
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromHours(1);

            if (_defaultExpiration <= TimeSpan.Zero)
                throw new ArgumentException("Default expiration must be greater than zero.", nameof(defaultExpiration));

            var interval = cleanupInterval ?? DefaultCleanupInterval;
            if (interval > TimeSpan.Zero)
            {
                _cleanupTimer = new Timer(
                    _ => RemoveExpiredTokens(),
                    null,
                    interval,
                    interval);
            }
            else
            {
                // Create a disabled timer (infinite due time)
                _cleanupTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Registers an observability token for a specific agent and tenant with default expiration.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">The observability token.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        public void RegisterObservability(string agentId, string tenantId, string token, string[] observabilityScopes)
        {
            RegisterObservability(agentId, tenantId, token, observabilityScopes, null);
        }

        /// <summary>
        /// Registers an observability token for a specific agent and tenant with custom expiration.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="token">The observability token.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        /// <param name="expiresIn">Optional custom expiration time. Uses default if not specified.</param>
        public void RegisterObservability(string agentId, string tenantId, string token, string[] observabilityScopes, TimeSpan? expiresIn)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(agentId));

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(token));

            if (observabilityScopes == null || observabilityScopes.Length == 0)
                throw new ArgumentException("Observability scopes cannot be null or empty.", nameof(observabilityScopes));

            var expiration = expiresIn ?? _defaultExpiration;
            if (expiration <= TimeSpan.Zero)
                throw new ArgumentException("Expiration time must be greater than zero.", nameof(expiresIn));

            var expiresAt = DateTimeOffset.UtcNow.Add(expiration);
            var entry = new Entry(token, observabilityScopes, expiresAt);

            var key = GetKey(agentId, tenantId);
            _map.AddOrUpdate(key, entry, (k, old) => entry);
        }

        /// <summary>
        /// Retrieves the observability token for a specific agent and tenant.
        /// Returns null if the token is not found or has expired.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>The observability token if valid; otherwise, null.</returns>
        public async Task<string?> GetObservabilityToken(string agentId, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(tenantId))
                return null;

            var key = GetKey(agentId, tenantId);

            if (!_map.TryGetValue(key, out var entry))
                return null;

            // Check if token has expired
            if (DateTimeOffset.UtcNow >= entry.ExpiresAt)
            {
                // Remove expired token
                _map.TryRemove(key, out _);
                return null;
            }

            return await Task.FromResult(entry.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Invalidates (removes) the cached token for a specific agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>True if the token was found and removed; otherwise, false.</returns>
        public bool InvalidateToken(string agentId, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(tenantId))
                return false;

            var key = GetKey(agentId, tenantId);
            return _map.TryRemove(key, out _);
        }

        /// <summary>
        /// Invalidates (removes) all cached tokens.
        /// </summary>
        public void InvalidateAll()
        {
            _map.Clear();
        }

        /// <summary>
        /// Removes all expired tokens from the cache.
        /// </summary>
        /// <returns>The number of expired tokens that were removed.</returns>
        public int RemoveExpiredTokens()
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = _map.Where(kvp => now >= kvp.Value.ExpiresAt)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

            int removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_map.TryRemove(key, out _))
                    removedCount++;
            }

            return removedCount;
        }

        /// <summary>
        /// Gets the current number of tokens in the cache.
        /// </summary>
        /// <returns>The number of tokens currently cached.</returns>
        public int Count => _map.Count;

        /// <summary>
        /// Disposes the cache and stops the automatic cleanup timer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the cache resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cleanupTimer?.Dispose();
                InvalidateAll();
            }

            _disposed = true;
        }

        private static string GetKey(string agentId, string tenantId) => $"{agentId}:{tenantId}";
    }
}
