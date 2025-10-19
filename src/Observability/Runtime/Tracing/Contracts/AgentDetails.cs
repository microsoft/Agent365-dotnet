using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Details about an AI agent in the system.
    /// </summary>
    public class AgentDetails : IEquatable<AgentDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AgentDetails"/> class.
        /// </summary>
        /// <param name="agentId">The unique identifier for the agent.</param>
        /// <param name="conversationId">Optional conversation identifier to associate with the agent.</param>
        /// <param name="agentName">Optional display name for the agent.</param>
        /// <param name="agentDescription">Optional description of the agent's purpose.</param>
        /// <param name="iconUri">Optional URI pointing to the agent icon.</param>
        public AgentDetails(string agentId, string? conversationId = null, string? agentName = null, string? agentDescription = null, string? iconUri = null)
        {
            AgentId = agentId;
            ConversationId = conversationId;
            AgentName = agentName;
            AgentDescription = agentDescription;
            IconUri = iconUri;
        }

        /// <summary>
        /// The unique identifier for the AI agent.
        /// </summary>
        public string AgentId { get; }

        /// <summary>
        /// The identifier for the conversation or session.
        /// </summary>
        public string? ConversationId { get; }

        /// <summary>
        /// The human-readable name of the AI agent.
        /// </summary>
        public string? AgentName { get; }

        /// <summary>
        /// A description of the AI agent's purpose or capabilities.
        /// </summary>
        public string? AgentDescription { get; }

        /// <summary>
        /// Optional icon identifier or URL for visual representation of the agent.
        /// </summary>
        public string? IconUri { get; }

        /// <summary>
        /// Deconstructs the current instance into discrete values.
        /// </summary>
        /// <param name="agentId">Receives the agent identifier.</param>
        /// <param name="conversationId">Receives the conversation identifier.</param>
        /// <param name="agentName">Receives the human-readable agent name.</param>
        /// <param name="agentDescription">Receives the agent description.</param>
        /// <param name="iconUri">Receives the icon URI.</param>
        public void Deconstruct(out string agentId, out string? conversationId, out string? agentName, out string? agentDescription, out string? iconUri)
        {
            agentId = AgentId;
            conversationId = ConversationId;
            agentName = AgentName;
            agentDescription = AgentDescription;
            iconUri = IconUri;
        }

        /// <inheritdoc/>
        public bool Equals(AgentDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(AgentId, other.AgentId, StringComparison.Ordinal) &&
                   string.Equals(ConversationId, other.ConversationId, StringComparison.Ordinal) &&
                   string.Equals(AgentName, other.AgentName, StringComparison.Ordinal) &&
                   string.Equals(AgentDescription, other.AgentDescription, StringComparison.Ordinal) &&
                   string.Equals(IconUri, other.IconUri, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as AgentDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (AgentId != null ? StringComparer.Ordinal.GetHashCode(AgentId) : 0);
                hash = (hash * 31) + (ConversationId != null ? StringComparer.Ordinal.GetHashCode(ConversationId) : 0);
                hash = (hash * 31) + (AgentName != null ? StringComparer.Ordinal.GetHashCode(AgentName) : 0);
                hash = (hash * 31) + (AgentDescription != null ? StringComparer.Ordinal.GetHashCode(AgentDescription) : 0);
                hash = (hash * 31) + (IconUri != null ? StringComparer.Ordinal.GetHashCode(IconUri) : 0);
                return hash;
            }
        }
    }
}