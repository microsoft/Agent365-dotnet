// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Tooling.Protection.Defender
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Resolves the access token for a Defender evaluation: the agent identity's own app-only token, in
    /// the agent's tenant, for the Defender API, carrying the <c>RealtimeProtection.Evaluate.All</c> role.
    /// </summary>
    /// <remarks>
    /// Use the same authority as Observability S2S export: the blueprint credential obtains the agent
    /// identity's assertion (FMI), and the agent identity exchanges it for the requested scope. The
    /// client caches the returned token per agent, tenant and scope until shortly before it expires.
    /// </remarks>
    /// <param name="agentId">The agent identity (application) id; the token's <c>appid</c>.</param>
    /// <param name="tenantId">The agent's tenant; the token's <c>tid</c> must equal the context <c>tenant.id</c>.</param>
    /// <param name="scopes">The scopes to request.</param>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <returns>The access token, or null when none is available.</returns>
    public delegate Task<string?> DefenderRtpTokenResolver(
        string agentId,
        string tenantId,
        string[] scopes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Identity of the agent and turn an evaluation is for. Fills context fields a host did not set.
    /// </summary>
    public sealed class DefenderRtpAgentContext
    {
        /// <summary>The agent identity (application) id the token is requested for.</summary>
        public string AgentId { get; set; } = string.Empty;

        /// <summary>The agent's tenant id; sent as <c>tenant.id</c> and used to acquire the token.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The agent's Entra object id, sent as <c>agent.id</c>. Defaults to the context's <c>agent.id</c>,
        /// then <see cref="AgentId"/> (equal for Agent ID agent identities).
        /// </summary>
        public string? AgentObjectId { get; set; }

        /// <summary>The agent's display name (<c>agent.name</c>) when the context has none.</summary>
        public string? AgentName { get; set; }

        /// <summary>The agent framework (<c>agent.framework</c>, lowercase <c>[a-z0-9_-]</c>) when the context has none.</summary>
        public string? Framework { get; set; }

        /// <summary>The turn's request id (<c>request_id</c>), for example the activity id.</summary>
        public string? RequestId { get; set; }

        /// <summary>Who triggered the run (<c>actor.id</c>), for example the user's Entra object id.</summary>
        public string? UserId { get; set; }

        /// <summary>
        /// The kind of actor (<c>actor.kind</c>): <c>human</c> (default), <c>service</c> for autonomous runs,
        /// or <c>agent</c> for agent-to-agent calls.
        /// </summary>
        public string? ActorKind { get; set; }

        /// <summary>The model the agent uses (<c>model.id</c>) when the context has none.</summary>
        public string? ModelName { get; set; }
    }
}
