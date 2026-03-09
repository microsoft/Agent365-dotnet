// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Core;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
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
            public string? Token { get; set; }
            public string[] Scopes { get; }
            public DateTimeOffset? ExpiresAt { get; set; }

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
    }
}
