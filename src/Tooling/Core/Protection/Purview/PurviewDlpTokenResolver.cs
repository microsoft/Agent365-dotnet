// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Purview
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Resolves the Microsoft Graph token for a Purview evaluation, and whose behalf the content is evaluated on.
    /// </summary>
    /// <remarks>
    /// The client does not cache what the resolver returns: the resolver owns caching.
    /// <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/> caches the agentic user's tokens;
    /// <see cref="PurviewDlpTokenResolvers.FromAccessTokenProvider"/> never caches, since its tokens may be for a user the
    /// agent context does not identify. The resolver is called within the evaluation's deadline
    /// (<see cref="PurviewDlpOptions.Timeout"/>), on the thread pool.
    /// </remarks>
    /// <param name="agent">The agent and turn being evaluated.</param>
    /// <param name="scope">The scope to request, <see cref="PurviewDlpOptions.AuthenticationScope"/>.</param>
    /// <param name="cancellationToken">Cancels the acquisition when the evaluation's deadline passes.</param>
    /// <returns>The token, or null when none is available.</returns>
    public delegate Task<PurviewDlpToken?> PurviewDlpTokenResolver(
        PurviewDlpAgentContext agent,
        string scope,
        CancellationToken cancellationToken);

    /// <summary>A Microsoft Graph access token, and the user the content is evaluated for.</summary>
    public sealed class PurviewDlpToken
    {
        /// <summary>Initializes a new instance of the <see cref="PurviewDlpToken"/> class.</summary>
        /// <param name="accessToken">The Microsoft Graph access token.</param>
        /// <param name="userId">
        /// The user to evaluate for, which selects <c>/users/{userId}</c>; null selects <c>/me</c>, the token's own
        /// user, as for a delegated token.
        /// </param>
        /// <exception cref="ArgumentException">The access token is empty, or the user id is empty but not null.</exception>
        public PurviewDlpToken(string accessToken, string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("The access token is required.", nameof(accessToken));
            }

            if (userId != null && string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("The user id must not be empty; pass null to evaluate as /me.", nameof(userId));
            }

            AccessToken = accessToken;
            UserId = userId;
        }

        /// <summary>The Microsoft Graph access token.</summary>
        public string AccessToken { get; }

        /// <summary>The user to evaluate for (<c>/users/{UserId}</c>), or null for the token's own user (<c>/me</c>).</summary>
        public string? UserId { get; }
    }

    /// <summary>The agent and turn a Purview evaluation is for.</summary>
    public sealed class PurviewDlpAgentContext
    {
        /// <summary>
        /// The agent identity (agent instance application) id, for example <c>turnContext.Activity.GetAgenticInstanceId()</c>.
        /// Sent as the agent's <c>identifier</c>, and used by <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/>.
        /// </summary>
        public string? AgentId { get; set; }

        /// <summary>
        /// The agent's tenant id, for example <c>turnContext.Activity.GetAgenticTenantId()</c>. Used by
        /// <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/>.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The agentic user's object id, for example <c>turnContext.Activity.GetAgenticUser()</c>. Used by
        /// <see cref="PurviewDlpTokenResolvers.FromAgenticUser"/>.
        /// </summary>
        public string? AgenticUserId { get; set; }

        /// <summary>The agent blueprint's application id, sent as the agent's <c>blueprintId</c>.</summary>
        public string? BlueprintId { get; set; }

        /// <summary>
        /// The Entra application id Purview DLP policies are scoped to (<c>protectedAppMetadata.applicationLocation</c>).
        /// Defaults to <see cref="BlueprintId"/>, then <see cref="AgentId"/>.
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// The agent's display name, which names the app and the content in Purview; defaults to <see cref="AgentId"/>,
        /// then the application id, since Purview needs a name.
        /// </summary>
        public string? AgentName { get; set; }

        /// <summary>
        /// The conversation or session that groups the content (the content entry's <c>correlationId</c>), for example
        /// the conversation id. Required. Agent-hooks interceptors use the context's <c>session.id</c>.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The content's position in the session (<c>sequenceNumber</c>). When unset, the client numbers each
        /// session's evaluations in the order they are made. Agent-hooks interceptors use the context's
        /// <c>sequence</c>.
        /// </summary>
        public long? SequenceNumber { get; set; }

        /// <summary>
        /// A copy with the given session and sequence, for example those of an agent-hooks context; this instance is
        /// not modified, so one context can serve every evaluation of a turn.
        /// </summary>
        /// <param name="sessionId">The session id, or null to keep <see cref="SessionId"/>.</param>
        /// <param name="sequenceNumber">The sequence number, or null to keep <see cref="SequenceNumber"/>.</param>
        /// <returns>The copy.</returns>
        public PurviewDlpAgentContext WithSession(string? sessionId, long? sequenceNumber)
        {
            var copy = (PurviewDlpAgentContext)MemberwiseClone();
            copy.SessionId = string.IsNullOrWhiteSpace(sessionId) ? SessionId : sessionId;
            copy.SequenceNumber = sequenceNumber ?? SequenceNumber;
            return copy;
        }
    }
}
