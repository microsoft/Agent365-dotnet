// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Agents.A365.Observability.Runtime.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Caches app-only observability tokens per (agentId, tenantId) using the provided resolver.
    /// Includes automatic periodic cleanup that clears expired token values while keeping resolver registrations.
    /// </summary>
    public class AgenticTokenCache : IExporterTokenCache<ObservabilityTokenResolver>, IDisposable
    {
        private sealed class Entry
        {
            public ObservabilityTokenResolver TokenResolver { get; set; }
            public string? Token { get; set; }
            public string[] Scopes { get; set; }
            public DateTimeOffset? ExpiresAt { get; set; }
            public DateTimeOffset? AcquiredAt { get; set; }
            public SemaphoreSlim RefreshLock { get; } = new SemaphoreSlim(1, 1);

            public Entry(ObservabilityTokenResolver tokenResolver, string[] scopes)
            {
                TokenResolver = tokenResolver;
                Scopes = scopes;
            }

            /// <summary>
            /// Clears the cached token value for security purposes.
            /// </summary>
            public void ClearToken()
            {
                Token = null;
                ExpiresAt = null;
                AcquiredAt = null;
            }
        }

        private readonly ConcurrentDictionary<string, Entry> _map = new ConcurrentDictionary<string, Entry>();
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly Timer? _cleanupTimer;
        private int _disposed; // Using int for Interlocked operations
        private int _removedDelegatedRegistrationLogged;
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MaxOpaqueTokenAge = TimeSpan.FromHours(1);

        /// <summary>
        /// Default interval for automatic cleanup of expired tokens (5 minutes).
        /// </summary>
        public static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Initializes a new instance of the <see cref="AgenticTokenCache"/> class.
        /// </summary>
        /// <param name="cleanupInterval">The interval for automatic cleanup of expired tokens. Defaults to 5 minutes if not specified. Set to TimeSpan.Zero to disable automatic cleanup.</param>
        public AgenticTokenCache(TimeSpan? cleanupInterval = null)
            : this(cleanupInterval, () => DateTimeOffset.UtcNow)
        {
        }

        internal AgenticTokenCache(TimeSpan? cleanupInterval, Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
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
        /// Registers an app-only observability token resolver for the specified agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tokenGenerator">The app-only token resolver.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        /// <remarks>
        /// First registration wins. Repeated calls for the same agent and tenant are idempotent
        /// and do not replace the resolver or clear the cached token. Use
        /// <see cref="RefreshObservabilityToken(string, string, ObservabilityTokenResolver, string[])"/>
        /// to replace the resolver used by future refreshes.
        /// </remarks>
        public void RegisterObservability(string agentId, string tenantId, ObservabilityTokenResolver tokenGenerator, string[] observabilityScopes)
        {
            ValidateAgentAndTenant(agentId, tenantId);
            if (tokenGenerator == null)
            {
                throw new ArgumentNullException(nameof(tokenGenerator));
            }

            var scopes = ValidateScopes(observabilityScopes);
            var entry = new Entry(tokenGenerator, scopes);
            _map.TryAdd(GetKey(agentId, tenantId), entry);
        }

        /// <summary>
        /// Delegated OBS token acquisition was removed. This overload is retained only to produce a compile-time error.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tokenGenerator">The removed delegated token generator.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        [Obsolete("Delegated OBS token acquisition has been removed. Register an ObservabilityTokenResolver app-only callback instead.", error: true)]
        public void RegisterObservability(string agentId, string tenantId, AgenticTokenStruct tokenGenerator, string[] observabilityScopes)
        {
            if (Interlocked.Exchange(ref _removedDelegatedRegistrationLogged, 1) == 0)
            {
                Trace.TraceError("AgenticTokenCache.RegisterObservability with AgenticTokenStruct is removed. OBS export requires an app-only ObservabilityTokenResolver.");
            }
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
            if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            if (!_map.TryGetValue(GetKey(agentId, tenantId), out var entry))
            {
                return null;
            }

            if (IsTokenUsable(entry))
            {
                return entry.Token;
            }

            return await RefreshEntryAsync(agentId, tenantId, entry, replacementResolver: null, replacementScopes: null).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes and caches an app-only observability token for the specified agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tokenResolver">The app-only token resolver.</param>
        /// <returns>The refreshed token.</returns>
        public Task<string> RefreshObservabilityToken(string agentId, string tenantId, ObservabilityTokenResolver tokenResolver)
        {
            return RefreshObservabilityToken(agentId, tenantId, tokenResolver, EnvironmentUtils.GetObservabilityAuthenticationScope());
        }

        /// <summary>
        /// Refreshes and caches an app-only observability token for the specified agent and tenant.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="tokenResolver">The app-only token resolver.</param>
        /// <param name="observabilityScopes">The observability scopes.</param>
        /// <returns>The refreshed token.</returns>
        public async Task<string> RefreshObservabilityToken(string agentId, string tenantId, ObservabilityTokenResolver tokenResolver, string[] observabilityScopes)
        {
            ValidateAgentAndTenant(agentId, tenantId);
            if (tokenResolver == null)
            {
                throw new ArgumentNullException(nameof(tokenResolver));
            }

            var scopes = ValidateScopes(observabilityScopes);
            var key = GetKey(agentId, tenantId);
            var entry = _map.GetOrAdd(key, _ => new Entry(tokenResolver, scopes));

            return await RefreshEntryAsync(agentId, tenantId, entry, tokenResolver, scopes).ConfigureAwait(false);
        }

        private async Task<string> RefreshEntryAsync(
            string agentId,
            string tenantId,
            Entry entry,
            ObservabilityTokenResolver? replacementResolver,
            string[]? replacementScopes)
        {
            // Serialize refreshes per agent and tenant: concurrent callers share one acquisition, and a
            // failed refresh cannot clear a token that another caller cached while it was waiting.
            await entry.RefreshLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Only explicit refreshes replace the resolver; cache-driven refreshes use the current one.
                if (replacementResolver != null && replacementScopes != null)
                {
                    entry.TokenResolver = replacementResolver;
                    entry.Scopes = replacementScopes;
                }

                if (IsTokenUsable(entry))
                {
                    return entry.Token!;
                }

                try
                {
                    var token = await entry.TokenResolver(agentId, tenantId, entry.Scopes).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        throw new InvalidOperationException("The observability token resolver returned an empty token.");
                    }

                    entry.Token = token;
                    entry.ExpiresAt = GetTokenExpiration(token!);
                    entry.AcquiredAt = _utcNow();

                    return token!;
                }
                catch
                {
                    entry.ClearToken();
                    throw;
                }
            }
            finally
            {
                entry.RefreshLock.Release();
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

            var key = GetKey(agentId, tenantId);
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
        /// Clears all expired tokens from the cache. Resolver registrations are kept, so the next
        /// <see cref="GetObservabilityToken(string, string)"/> call acquires a new token.
        /// </summary>
        /// <returns>The number of expired tokens that were cleared.</returns>
        public int RemoveExpiredTokens()
        {
            var now = _utcNow();
            int removedCount = 0;

            foreach (var kvp in _map)
            {
                var entry = kvp.Value;

                // Skip entries with a refresh in flight; the next cleanup pass re-evaluates them.
                if (!entry.RefreshLock.Wait(0))
                {
                    continue;
                }

                try
                {
                    if (IsTokenExpired(entry, now))
                    {
                        // Clear the token value for security
                        entry.ClearToken();
                        removedCount++;
                    }
                }
                finally
                {
                    entry.RefreshLock.Release();
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

            if (token.Split('.').Length < 2)
            {
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken;
            try
            {
                jwtToken = handler.ReadJwtToken(token);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (jwtToken.Payload.Expiration == null)
            {
                return null;
            }

            return new DateTimeOffset(jwtToken.ValidTo, TimeSpan.Zero);
        }

        private static string GetKey(string agentId, string tenantId) => $"{agentId}:{tenantId}";

        private bool IsTokenUsable(Entry entry)
        {
            return !string.IsNullOrEmpty(entry.Token)
                && !IsTokenExpired(entry, _utcNow());
        }

        private static bool IsTokenExpired(Entry entry, DateTimeOffset now)
        {
            if (string.IsNullOrEmpty(entry.Token))
            {
                return false;
            }

            if (entry.ExpiresAt.HasValue)
            {
                return now >= entry.ExpiresAt.Value.Subtract(RefreshSkew);
            }

            if (entry.AcquiredAt.HasValue)
            {
                return now >= entry.AcquiredAt.Value.Add(MaxOpaqueTokenAge);
            }

            return true;
        }

        private static void ValidateAgentAndTenant(string agentId, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(agentId));
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(tenantId));
            }
        }

        private static string[] ValidateScopes(string[] observabilityScopes)
        {
            if (observabilityScopes == null || observabilityScopes.Length == 0)
            {
                throw new ArgumentException("Observability scopes cannot be null or empty.", nameof(observabilityScopes));
            }

            return (string[])observabilityScopes.Clone();
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
