// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System;
using System.Net;

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
        /// <param name="agentName">Optional display name for the agent.</param>
        /// <param name="agentDescription">Optional description of the agent's purpose.</param>
        /// <param name="iconUri">Optional URI pointing to the agent icon.</param>
        /// <param name="agentAUID">Optional Azure User ID (AUID) for the agent.</param>
        /// <param name="agentUPN">Optional User Principal Name (UPN) for the agent.</param>
        /// <param name="agentBlueprintId">Optional Blueprint/Application ID for the agent.</param>
        /// <param name="tenantId">Optional Tenant ID for the agent.</param>
        /// <param name="agentType">Optional agent type.</param>
        /// <param name="agentClientIP">Optional client IP address of the agent.</param>
        /// <param name="agentPlatformId">Optional platform ID for the agent.</param>
        /// <remarks>
        /// Either <paramref name="agentId"/> or <paramref name="agentPlatformId"/> should be provided to identify the agent.
        /// </remarks>
        public AgentDetails(
            string? agentId = null,
            string? agentName = null,
            string? agentDescription = null,
            string? iconUri = null,
            string? agentAUID = null,
            string? agentUPN = null,
            string? agentBlueprintId = null,
            string? tenantId = null,
            AgentType? agentType = null,
            IPAddress? agentClientIP = null,
            string? agentPlatformId = null)
        {
            AgentId = agentId;
            AgentName = agentName;
            AgentDescription = agentDescription;
            AgentAUID = agentAUID;
            AgentUPN = agentUPN;
            AgentBlueprintId = agentBlueprintId;
            TenantId = tenantId;
            AgentType = agentType;
            AgentClientIP = agentClientIP;
            AgentPlatformId = agentPlatformId;
        }

        /// <summary>
        /// The unique identifier for the AI agent.
        /// </summary>
        public string? AgentId { get; }

        /// <summary>
        /// The human-readable name of the AI agent.
        /// </summary>
        public string? AgentName { get; }

        /// <summary>
        /// Optional Agent User ID for the agent.
        /// </summary>
        public string? AgentAUID { get; }

        /// <summary>
        /// Optional User Principal Name (UPN) for the agent.
        /// </summary>
        public string? AgentUPN { get; }

        /// <summary>
        /// Optional Blueprint/Application ID for the agent.
        /// </summary>
        public string? AgentBlueprintId { get; }

        /// <summary>
        /// A description of the AI agent's purpose or capabilities.
        /// </summary>
        public string? AgentDescription { get; }

        /// <summary>
        /// The agent type. 
        /// </summary>
        public AgentType? AgentType { get; }

        /// <summary>
        /// Gets the client IP address of the agent.
        /// </summary>
        public IPAddress? AgentClientIP { get; }

        /// <summary>
        /// The optional platform ID for the agent.
        /// </summary>
        public string? AgentPlatformId { get; }

        /// <summary>
        /// Optional Tenant ID for the agent.
        /// </summary>
        public string? TenantId { get; }

        /// <summary>
        /// Deconstructs the current instance into discrete values.
        /// </summary>
        /// <param name="agentId">Receives the agent identifier.</param>
        /// <param name="agentName">Receives the human-readable agent name.</param>
        /// <param name="agentDescription">Receives the agent description.</param>
        /// <param name="agentAUID">Receives the agent Azure User ID (AUID).</param>
        /// <param name="agentUPN">Receives the agent User Principal Name (UPN).</param>
        /// <param name="agentBlueprintId">Receives the agent Blueprint/Application ID.</param>
        /// <param name="agentType">Receives the agent type.</param>
        /// <param name="tenantId">Receives the tenant identifier.</param>
        /// <param name="agentClientIP">Receives the client IP address.</param>
        /// <param name="agentPlatformId">Receives the platform ID.</param>
        public void Deconstruct(
            out string? agentId,
            out string? agentName,
            out string? agentDescription,
            out string? agentAUID,
            out string? agentUPN,
            out string? agentBlueprintId,
            out AgentType? agentType,
            out string? tenantId,
            out IPAddress? agentClientIP,
            out string? agentPlatformId)
        {
            agentId = AgentId;
            agentName = AgentName;
            agentDescription = AgentDescription;
            agentAUID = AgentAUID;
            agentUPN = AgentUPN;
            agentBlueprintId = AgentBlueprintId;
            agentType = AgentType;
            tenantId = TenantId;
            agentClientIP = AgentClientIP;
            agentPlatformId = AgentPlatformId;
        }

        /// <inheritdoc/>
        public bool Equals(AgentDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(AgentId, other.AgentId, StringComparison.Ordinal) &&
                   string.Equals(AgentName, other.AgentName, StringComparison.Ordinal) &&
                   string.Equals(AgentDescription, other.AgentDescription, StringComparison.Ordinal) &&
                   string.Equals(AgentAUID, other.AgentAUID, StringComparison.Ordinal) &&
                   string.Equals(AgentUPN, other.AgentUPN, StringComparison.Ordinal) &&
                   string.Equals(AgentBlueprintId, other.AgentBlueprintId, StringComparison.Ordinal) &&
                   AgentType == other.AgentType &&
                   string.Equals(TenantId, other.TenantId, StringComparison.Ordinal) &&
                   Equals(AgentClientIP, other.AgentClientIP) &&
                   string.Equals(AgentPlatformId, other.AgentPlatformId, StringComparison.Ordinal);
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
                hash = (hash * 31) + (AgentName != null ? StringComparer.Ordinal.GetHashCode(AgentName) : 0);
                hash = (hash * 31) + (AgentDescription != null ? StringComparer.Ordinal.GetHashCode(AgentDescription) : 0);
                hash = (hash * 31) + (AgentAUID != null ? StringComparer.Ordinal.GetHashCode(AgentAUID) : 0);
                hash = (hash * 31) + (AgentUPN != null ? StringComparer.Ordinal.GetHashCode(AgentUPN) : 0);
                hash = (hash * 31) + (AgentBlueprintId != null ? StringComparer.Ordinal.GetHashCode(AgentBlueprintId) : 0);
                hash = (hash * 31) + (AgentType?.GetHashCode() ?? 0);
                hash = (hash * 31) + (TenantId != null ? StringComparer.Ordinal.GetHashCode(TenantId) : 0);
                hash = (hash * 31) + (AgentClientIP?.GetHashCode() ?? 0);
                hash = (hash * 31) + (AgentPlatformId != null ? StringComparer.Ordinal.GetHashCode(AgentPlatformId) : 0);
                return hash;
            }
        }
    }
}