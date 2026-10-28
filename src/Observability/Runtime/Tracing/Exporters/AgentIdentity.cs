// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Represents the identity of an agent and its acting user.
    /// <para>
    /// <see cref="AgenticUserId"/> is contextual metadata only. OBS export must use an app-only
    /// token for <see cref="AgentId"/> and must not use this value for delegated token exchange.
    /// </para>
    /// </summary>
    public class AgentIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentIdentity"/> class.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="agenticUserId">The agentic user identifier (AAD Object ID), or null when unavailable.</param>
        public AgentIdentity(string agentId, string? agenticUserId = null)
        {
            AgentId = agentId;
            AgenticUserId = agenticUserId;
        }

        /// <summary>
        /// Gets the agent identifier.
        /// </summary>
        public string AgentId { get; }

        /// <summary>
        /// Gets the agentic user identifier (AAD Object ID), if available.
        /// This value is not an OBS authentication input.
        /// </summary>
        public string? AgenticUserId { get; }
    }
}
