// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Provides contextual information to the token resolver delegate.
    /// <para>
    /// <see cref="Identity"/> provides first-class access to agent identity fields (agent ID,
    /// agentic user ID). <see cref="TenantId"/> and <see cref="Identity"/>
    /// together identify the cache key.
    /// </para>
    /// </summary>
    public class TokenResolverContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenResolverContext"/> class.
        /// </summary>
        /// <param name="identity">The agent identity associated with this request.</param>
        /// <param name="tenantId">The tenant identifier (cache key).</param>
        public TokenResolverContext(AgentIdentity identity, string tenantId)
        {
            Identity = identity;
            TenantId = tenantId;
        }

        /// <summary>
        /// Gets the agent identity associated with this token resolution request.
        /// Contains the agent ID and agentic user ID (AAD Object ID) as first-class properties.
        /// </summary>
        public AgentIdentity Identity { get; }

        /// <summary>
        /// Gets the tenant identifier (part of the cache key).
        /// </summary>
        public string TenantId { get; }
    }
}
