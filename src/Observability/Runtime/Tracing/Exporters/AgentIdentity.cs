// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Exporters
{
    /// <summary>
    /// Represents the identity of an agent and its acting user.
    /// <para>
    /// In the AI teammate scenario, <see cref="AgenticUserId"/> is 1:1 with <see cref="AgentId"/>.
    /// In the S2S scenario, <see cref="AgenticUserId"/> will be null.
    /// </para>
    /// </summary>
    public class AgentIdentity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentIdentity"/> class.
        /// </summary>
        /// <param name="agentId">The agent identifier.</param>
        /// <param name="agenticUserId">The agentic user identifier (AAD Object ID), or null in S2S scenarios.</param>
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
        /// Gets the agentic user identifier (AAD Object ID).
        /// In the AI teammate scenario, this value is 1:1 with <see cref="AgentId"/>.
        /// Will be null in the S2S scenario.
        /// </summary>
        public string? AgenticUserId { get; }
    }
}
