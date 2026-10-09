// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Agents.Authentication;

    /// <summary>Token resolvers for <see cref="PurviewDlpClient"/>.</summary>
    public static class PurviewDlpTokenResolvers
    {
        /// <summary>How long one agentic user token acquisition may take, shared by the evaluations waiting for it.</summary>
        internal static readonly TimeSpan AcquisitionTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The agentic user's delegated Microsoft Graph token, issued through the agent's Agents SDK connection, so
        /// content is evaluated as the agent's agentic user (<c>/me</c>). The connection's blueprint credential issues the
        /// agent identity's assertion and exchanges it for the agentic user's token
        /// (<see cref="IAgenticTokenProvider.GetAgenticUserTokenAsync"/>). The token needs the delegated permission
        /// <c>Content.Process.User</c>, which agentic users inherit from their blueprint's Microsoft Graph grant.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The evaluation's <see cref="PurviewDlpAgentContext"/> must carry <see cref="PurviewDlpAgentContext.TenantId"/>,
        /// <see cref="PurviewDlpAgentContext.AgentId"/> and <see cref="PurviewDlpAgentContext.AgenticUserId"/>, for
        /// example from the incoming activity: <c>GetAgenticTenantId()</c>, <c>GetAgenticInstanceId()</c> and
        /// <c>GetAgenticUser()</c>. Without them the resolver throws, and the evaluation follows the fail mode.
        /// </para>
        /// <para>
        /// The resolver caches each token per tenant, agent identity, agentic user and scope until it expires, and
        /// refreshes it in the background within five minutes of expiry, keeping the cached token while it is still
        /// valid, so connections that do not cache are not asked on every evaluation. Concurrent evaluations share one
        /// acquisition, bounded by thirty seconds rather than any evaluation's deadline, so a slow first acquisition
        /// still fills the cache; failures are never cached.
        /// </para>
        /// </remarks>
        /// <param name="connection">
        /// The agent's connection, for example <c>connections.GetDefaultConnection()</c> cast to
        /// <see cref="IAgenticTokenProvider"/> (MSAL connections implement it).
        /// </param>
        /// <returns>A resolver for <see cref="PurviewDlpClient.EvaluateAsync"/>.</returns>
        public static PurviewDlpTokenResolver FromAgenticUser(IAgenticTokenProvider connection) =>
            CreateAgenticUserResolver(connection, TimeProvider.System, AcquisitionTimeout);

        /// <summary>The agentic user resolver with the given clock and acquisition timeout; for tests.</summary>
        internal static PurviewDlpTokenResolver CreateAgenticUserResolver(
            IAgenticTokenProvider connection,
            TimeProvider timeProvider,
            TimeSpan acquisitionTimeout)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            var cache = new AgenticUserTokenCache(
                async (key, cancellationToken) => await connection
                    .GetAgenticUserTokenAsync(key.TenantId, key.AgentId, key.AgenticUserId, new List<string> { key.Scope }, cancellationToken)
                    .ConfigureAwait(false),
                timeProvider,
                acquisitionTimeout);
            return async (agent, scope, cancellationToken) =>
            {
                if (agent == null)
                {
                    throw new ArgumentNullException(nameof(agent));
                }

                var key = new AgenticUserTokenKey(
                    Require(agent.TenantId, nameof(PurviewDlpAgentContext.TenantId)),
                    Require(agent.AgentId, nameof(PurviewDlpAgentContext.AgentId)),
                    Require(agent.AgenticUserId, nameof(PurviewDlpAgentContext.AgenticUserId)),
                    scope);
                var token = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(token) ? null : new PurviewDlpToken(token!);
            };
        }

        /// <summary>
        /// A Microsoft Graph token from the host's own token provider, for example an on-behalf-of token for the
        /// signed-in user, which evaluates as <c>/me</c>. With <paramref name="userId"/>, content is evaluated for that
        /// user (<c>/users/{userId}</c>), as an app-only token with the application permission
        /// <c>Content.Process.All</c> requires; that path has not been validated end to end yet. Its tokens are never
        /// cached here, since they may be for a user the agent context does not identify: the provider should cache
        /// them, as the client asks for one on every evaluation.
        /// </summary>
        /// <param name="getToken">Returns an access token for the given scope; it observes the cancellation token.</param>
        /// <param name="userId">The user to evaluate for, or null for the token's own user.</param>
        /// <returns>A resolver for <see cref="PurviewDlpClient.EvaluateAsync"/>.</returns>
        /// <exception cref="ArgumentException">The user id is empty but not null.</exception>
        public static PurviewDlpTokenResolver FromAccessTokenProvider(
            Func<string, CancellationToken, Task<string>> getToken,
            string? userId = null)
        {
            if (getToken == null)
            {
                throw new ArgumentNullException(nameof(getToken));
            }

            if (userId != null && string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("The user id must not be empty; pass null to evaluate as /me.", nameof(userId));
            }

            return async (_, scope, cancellationToken) =>
            {
                var token = await getToken(scope, cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(token) ? null : new PurviewDlpToken(token, userId);
            };
        }

        private static string Require(string? value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException($"The agentic user token requires {name}.", name)
                : value!;
    }
}
