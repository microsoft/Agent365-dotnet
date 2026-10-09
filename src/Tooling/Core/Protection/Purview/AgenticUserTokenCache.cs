// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Identifies an agentic user token by its parts, so no two users, agents or scopes can share one.</summary>
    internal readonly record struct AgenticUserTokenKey(string TenantId, string AgentId, string AgenticUserId, string Scope);

    /// <summary>
    /// Caches agentic user tokens per tenant, agent identity, agentic user and scope until they expire, and refreshes a
    /// token in the background within five minutes of its expiry while the cached one stays in use until it actually
    /// expires. Concurrent requests for one key share an acquisition, which is bounded by its own timeout rather than
    /// any caller's cancellation and leaves the in-flight map as it completes, so a failure is never handed to a later
    /// caller and never cached. A token whose expiry cannot be read is used but not cached.
    /// </summary>
    internal sealed class AgenticUserTokenCache
    {
        /// <summary>The most tokens the cache holds.</summary>
        internal const int MaxEntries = 100;

        private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

        private readonly Func<AgenticUserTokenKey, CancellationToken, Task<string?>> _acquire;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _acquisitionTimeout;
        private readonly ConcurrentDictionary<AgenticUserTokenKey, CachedToken> _tokens = new();
        private readonly ConcurrentDictionary<AgenticUserTokenKey, Lazy<Task<string?>>> _inFlight = new();

        // Serializes writes to _tokens, so concurrent acquisitions cannot push it past MaxEntries.
        private readonly object _writeLock = new();

        /// <summary>Initializes a new instance of the <see cref="AgenticUserTokenCache"/> class.</summary>
        /// <param name="acquire">Acquires a token for a key; it observes the cancellation token.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="acquisitionTimeout">How long one acquisition may take.</param>
        public AgenticUserTokenCache(
            Func<AgenticUserTokenKey, CancellationToken, Task<string?>> acquire,
            TimeProvider timeProvider,
            TimeSpan acquisitionTimeout)
        {
            _acquire = acquire;
            _timeProvider = timeProvider;
            _acquisitionTimeout = acquisitionTimeout;
        }

        /// <summary>The number of cached tokens; for tests.</summary>
        internal int Count => _tokens.Count;

        /// <summary>
        /// The token for the key: the cached one until it expires, or the result of an acquisition, which the caller
        /// waits for until it is cancelled.
        /// </summary>
        /// <param name="key">The token's tenant, agent identity, agentic user and scope.</param>
        /// <param name="cancellationToken">Stops waiting; the acquisition itself goes on, so its token is cached.</param>
        /// <returns>The token, or null when the connection returned none.</returns>
        public async Task<string?> GetAsync(AgenticUserTokenKey key, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();
            if (_tokens.TryGetValue(key, out var cached) && now < cached.ExpiresAt)
            {
                if (now >= cached.ExpiresAt - RefreshSkew)
                {
                    // A failed refresh is retried by a later call; the cached token stays in use until it expires.
                    ObserveFailure(StartOrJoin(key));
                }

                return cached.Token;
            }

            return await StartOrJoin(key).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The acquisition in flight for the key, if any; for tests. Its task completes only after the acquisition has
        /// left the in-flight map, so awaiting it orders a later call after it.
        /// </summary>
        internal Task? Pending(AgenticUserTokenKey key) =>
            _inFlight.TryGetValue(key, out var acquisition) ? acquisition.Value : null;

        /// <summary>Observes the failure of a task no caller awaits, so it is not reported as unobserved.</summary>
        private static void ObserveFailure(Task task) =>
            _ = task.ContinueWith(
                static completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

        private static DateTimeOffset? ReadExpiry(string token)
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
                if ((JsonNode.Parse(Convert.FromBase64String(payload)) as JsonObject)?["exp"] is not JsonValue exp)
                {
                    return null;
                }

                if (exp.TryGetValue<long>(out var seconds))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(seconds);
                }

                return exp.TryGetValue<double>(out var fractional)
                    ? DateTimeOffset.FromUnixTimeSeconds((long)fractional)
                    : null;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return null;
            }
        }

        private Task<string?> StartOrJoin(AgenticUserTokenKey key)
        {
            while (true)
            {
                if (_inFlight.TryGetValue(key, out var inFlight))
                {
                    return inFlight.Value;
                }

                Lazy<Task<string?>>? acquisition = null;
                acquisition = new Lazy<Task<string?>>(() => AcquireAsync(key, acquisition!));
                if (_inFlight.TryAdd(key, acquisition))
                {
                    return acquisition.Value;
                }
            }
        }

        private async Task<string?> AcquireAsync(AgenticUserTokenKey key, Lazy<Task<string?>> acquisition)
        {
            try
            {
                using var timeout = new CancellationTokenSource(_acquisitionTimeout, _timeProvider);

                // Started on the thread pool, so a connection that blocks before it returns its task holds no caller,
                // and bounded by the timeout as well as asked to stop, so one that ignores cancellation cannot hold the
                // key's acquisition forever.
                var acquiring = Task.Run(() => _acquire(key, timeout.Token), CancellationToken.None);
                string? token;
                try
                {
                    token = await acquiring.WaitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ObserveFailure(acquiring);
                    throw;
                }

                if (!string.IsNullOrWhiteSpace(token) && ReadExpiry(token!) is { } expiresAt)
                {
                    if (expiresAt <= _timeProvider.GetUtcNow())
                    {
                        throw new InvalidOperationException("The connection returned an expired agentic user token.");
                    }

                    Store(key, new CachedToken(token!, expiresAt));
                }

                return token;
            }
            finally
            {
                // Runs before the acquisition's task completes, so no caller joins a finished or failed one.
                _inFlight.TryRemove(new KeyValuePair<AgenticUserTokenKey, Lazy<Task<string?>>>(key, acquisition));
            }
        }

        /// <summary>
        /// Caches a token within <see cref="MaxEntries"/>: at capacity, expired tokens go first, then the one closest to
        /// expiry.
        /// </summary>
        private void Store(AgenticUserTokenKey key, CachedToken token)
        {
            lock (_writeLock)
            {
                if (!_tokens.ContainsKey(key) && _tokens.Count >= MaxEntries)
                {
                    var now = _timeProvider.GetUtcNow();
                    foreach (var stale in _tokens.Where(entry => entry.Value.ExpiresAt <= now).Select(entry => entry.Key).ToList())
                    {
                        _tokens.TryRemove(stale, out _);
                    }

                    if (_tokens.Count >= MaxEntries && _tokens.OrderBy(entry => entry.Value.ExpiresAt).FirstOrDefault() is { Value: not null } oldest)
                    {
                        _tokens.TryRemove(oldest.Key, out _);
                    }
                }

                _tokens[key] = token;
            }
        }

        private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);
    }
}
