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
        /// <summary>
        /// The agentic user's delegated Microsoft Graph token, issued through the agent's Agents SDK connection, so
        /// content is evaluated as the agent's agentic user (<c>/me</c>). The connection's blueprint credential issues the
        /// agent identity's assertion and exchanges it for the agentic user's token
        /// (<see cref="IAgenticTokenProvider.GetAgenticUserTokenAsync"/>); MSAL connections cache each token of that
        /// chain. The token needs the delegated permission <c>Content.Process.User</c>, which agentic users inherit
        /// from their blueprint's Microsoft Graph grant.
        /// </summary>
        /// <remarks>
        /// The evaluation's <see cref="PurviewDlpAgentContext"/> must carry <see cref="PurviewDlpAgentContext.TenantId"/>,
        /// <see cref="PurviewDlpAgentContext.AgentId"/> and <see cref="PurviewDlpAgentContext.AgenticUserId"/>, for
        /// example from the incoming activity: <c>GetAgenticTenantId()</c>, <c>GetAgenticInstanceId()</c> and
        /// <c>GetAgenticUser()</c>. Without them the resolver throws, and the evaluation follows the fail mode.
        /// </remarks>
        /// <param name="connection">
        /// The agent's connection, for example <c>connections.GetDefaultConnection()</c> cast to
        /// <see cref="IAgenticTokenProvider"/> (MSAL connections implement it).
        /// </param>
        /// <returns>A resolver for <see cref="PurviewDlpClient.EvaluateAsync"/>.</returns>
        public static PurviewDlpTokenResolver FromAgenticUser(IAgenticTokenProvider connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            return async (agent, scope, cancellationToken) =>
            {
                if (agent == null)
                {
                    throw new ArgumentNullException(nameof(agent));
                }

                var tenantId = Require(agent.TenantId, nameof(PurviewDlpAgentContext.TenantId));
                var agentId = Require(agent.AgentId, nameof(PurviewDlpAgentContext.AgentId));
                var agenticUserId = Require(agent.AgenticUserId, nameof(PurviewDlpAgentContext.AgenticUserId));
                var token = await connection
                    .GetAgenticUserTokenAsync(tenantId, agentId, agenticUserId, new List<string> { scope }, cancellationToken)
                    .ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(token) ? null : new PurviewDlpToken(token);
            };
        }

        /// <summary>
        /// A Microsoft Graph token from the host's own token provider, for example an on-behalf-of token for the
        /// signed-in user, which evaluates as <c>/me</c>. With <paramref name="userId"/>, content is evaluated for that
        /// user (<c>/users/{userId}</c>), as an app-only token with the application permission
        /// <c>Content.Process.All</c> requires; that path has not been validated end to end yet. The provider should
        /// cache its tokens: the client asks for one on every evaluation.
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
