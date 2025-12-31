// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Microsoft.Agents.A365.Observability.Hosting.Caching
{
    /// <summary>
    /// Caches observability tokens per (agentId, tenantId) using the provided UserAuthorization and TurnContext.
    /// </summary>
    public class AgenticTokenCache : IExporterTokenCache<AgenticTokenStruct>
    {
        private sealed class Entry
        {
            public AgenticTokenStruct AgenticTokenStruct { get; }
            public string[] Scopes { get; }

            public Entry(AgenticTokenStruct agenticTokenStruct, string[] scopes)
            {
                AgenticTokenStruct = agenticTokenStruct;
                Scopes = scopes;
            }
        }
        private readonly ConcurrentDictionary<string, Entry> _map = new ConcurrentDictionary<string, Entry>();

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
                // Use sync path; credential handles caching & refresh internally.
                var ctx = new TokenRequestContext(entry.Scopes);
                var userAuthorization = entry.AgenticTokenStruct.UserAuthorization;
                var turnContext = entry.AgenticTokenStruct.TurnContext;

                var token = await userAuthorization.ExchangeTurnTokenAsync(turnContext,
                        entry.AgenticTokenStruct.AuthHandlerName,
                        exchangeConnection: entry.AgenticTokenStruct.ConnectionName!,
                        exchangeScopes: entry.Scopes).ConfigureAwait(false);

                return token;
            }
            catch
            {
                return null;
            }
        }
    }
}
