// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Caches observability tokens per (agentId, tenantId) using the provided UserAuthorization and TurnContext.
    /// Includes automatic periodic cleanup of expired tokens for improved memory management.
    /// </summary>
    public class AgenticTokenCache : IExporterTokenCache<AgenticTokenStruct>, IDisposable
    {
        private sealed class Entry
        {
            public AgenticTokenStruct AgenticTokenStruct { get; }
            public string? Token { get; set; }
            public string[] Scopes { get; }
            public DateTimeOffset? ExpiresAt { get; set; }

            public Entry(AgenticTokenStruct agenticTokenStruct, string[] scopes)
            {
                AgenticTokenStruct = agenticTokenStruct;
                Scopes = scopes;
            }

            /// <summary>
            /// Clears the cached token value for security purposes.
            /// </summary>
            public void ClearToken()
            {
                Token = null;
                ExpiresAt = null;
            }
        }

        private readonly ConcurrentDictionary<string, Entry> _map = new ConcurrentDictionary<string, Entry>();
        private readonly Timer? _cleanupTimer;
        private int _disposed; // Using int for Interlocked operations

        /// <summary>
        /// Default interval for automatic cleanup of expired tokens (5 minutes).
        /// </summary>
        public static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="AgenticTokenCache"/> class.
        /// </summary>
        /// <param name="cleanupInterval">The interval for automatic cleanup of expired tokens. Defaults to 5 minutes if not specified. Set to TimeSpan.Zero to disable automatic cleanup.</param>
        public AgenticTokenCache(TimeSpan? cleanupInterval = null)
        {
            var interval = cleanupInterval ?? DefaultCleanupInterval;
            if (interval > TimeSpan.Zero)
            {
                _cleanupTimer = new Timer(
                    _ => RemoveExpiredTokens(),
                    null,
                    interval,
                    interval);
            }
            // When interval <= TimeSpan.Zero, _cleanupTimer remains null (no automatic cleanup)
        }

        /// <summary>
        /// Registers observability for the specified agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tokenGenerator">The token generator.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        public void RegisterObservability(string agentId, string tenantId, AgenticTokenStruct tokenGenerator, string[] observabilityScopes)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(agentId));

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(tenantId));

            if (tokenGenerator == null)
                throw new ArgumentNullException(nameof(tokenGenerator));

            // First registration wins; subsequent calls ignored (idempotent).
            _map.TryAdd($"{agentId}:{tenantId}", new Entry(tokenGenerator, observabilityScopes));
        }

        /// <summary>
        /// Gets the observability token for the specified agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>
        /// The observability token if available; otherwise, <c>null</c>.
        /// </returns>
        public async Task<string?> GetObservabilityToken(string agentId, string tenantId)
        {
            if (!_map.TryGetValue($"{agentId}:{tenantId}", out var entry))
                return null;

            try
            {
                // Check current entry to avoid unnecessary token exchange calls if the token is still valid.
                if (!string.IsNullOrEmpty(entry.Token) && entry.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5)) // Consider token valid if it expires in more than 5 minutes.
                {
                    return entry.Token;
                }

                // Use sync path; credential handles caching & refresh internally.
                var ctx = new TokenRequestContext(entry.Scopes);
                var userAuthorization = entry.AgenticTokenStruct.UserAuthorization;
                var turnContext = entry.AgenticTokenStruct.TurnContext;

                var token = await userAuthorization.ExchangeTurnTokenAsync(turnContext,
                        entry.AgenticTokenStruct.AuthHandlerName,
                        exchangeConnection: entry.AgenticTokenStruct.ConnectionName!,
                        exchangeScopes: entry.Scopes).ConfigureAwait(false);

                entry.Token = token;
                entry.ExpiresAt = GetTokenExpiration(token);

                return token;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Invalidates (removes) the cached entry for a specific agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <returns>True if the entry was found and removed; otherwise, false.</returns>
        public bool InvalidateToken(string agentId, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(tenantId))
                return false;

            var key = $"{agentId}:{tenantId}";
            if (_map.TryRemove(key, out var entry))
            {
                // Clear the token value for security
                entry.ClearToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Invalidates (removes) all cached entries.
        /// </summary>
        public void InvalidateAll()
        {
            // Clear all token values before removing entries
            foreach (var kvp in _map)
            {
                kvp.Value.ClearToken();
            }
            _map.Clear();
        }

        /// <summary>
        /// Removes all expired tokens from the cache.
        /// </summary>
        /// <returns>The number of expired tokens that were removed.</returns>
        public int RemoveExpiredTokens()
        {
            var now = DateTimeOffset.UtcNow;
            var expiredKeys = new List<string>();

            // Find expired keys without using LINQ
            foreach (var kvp in _map)
            {
                if (kvp.Value.ExpiresAt.HasValue && now >= kvp.Value.ExpiresAt.Value)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            int removedCount = 0;
            foreach (var key in expiredKeys)
            {
                if (_map.TryRemove(key, out var entry))
                {
                    // Clear the token value for security
                    entry.ClearToken();
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Gets the current number of entries in the cache.
        /// </summary>
        /// <returns>The number of entries currently cached.</returns>
        public int Count => _map.Count;

        /// <summary>
        /// Extracts the expiration date and time from a JWT access token.
        /// </summary>
        /// <remarks>The returned expiration is based on the 'exp' claim in the token payload. No
        /// validation of token signature or claims is performed; callers should ensure the token is trusted before
        /// relying on the expiration value.</remarks>
        /// <param name="token">The JWT access token from which to retrieve the expiration information. Cannot be null, empty, or
        /// whitespace.</param>
        /// <returns>A <see cref="DateTimeOffset"/> representing the token's expiration date and time, or <see langword="null"/>
        /// if the token is null, empty, or whitespace.</returns>
        private static DateTimeOffset? GetTokenExpiration(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            if (jwtToken.Payload.Expiration == null)
                return null; 

            return new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero);
        }

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
            // Thread-safe disposal check using Interlocked
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            if (disposing)
            {
                _cleanupTimer?.Dispose();
                InvalidateAll();
            }
        }
    }
}
