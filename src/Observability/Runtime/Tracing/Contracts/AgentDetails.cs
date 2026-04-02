// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// <param name="agenticUserId">Optional agentic user ID for the agent.</param>
        /// <param name="agenticUserEmail">Optional email address for the agentic user.</param>
        /// <param name="agentBlueprintId">Optional Blueprint/Application ID for the agent.</param>
        /// <param name="tenantId">Optional Tenant ID for the agent.</param>
        /// <param name="agentType">Optional agent type.</param>
        /// <param name="agentClientIP">Optional client IP address of the agent.</param>
        /// <param name="agentPlatformId">Optional platform ID for the agent.</param>
        /// <param name="providerName">Optional provider name (e.g., openai, anthropic).</param>
        /// <param name="agentVersion">Optional version of the agent (e.g., "1.0.0", "2025-05-01").</param>
        /// <remarks>
        /// <para>
        /// <b>Certification Requirements:</b> The following parameters must be set for the agent to pass certification requirements, and these values override any of the same values specified in the <see cref="Microsoft.Agents.A365.Observability.Runtime.Common.BaggageBuilder"/> class:
        /// <list type="bullet">
        ///   <item><paramref name="agentId"/></item>
        ///   <item><paramref name="agentName"/></item>
        ///   <item><paramref name="agentDescription"/></item>
        ///   <item><paramref name="agenticUserId"/></item>
        ///   <item><paramref name="agenticUserEmail"/></item>
        ///   <item><paramref name="agentBlueprintId"/></item>
        /// </list>
        /// </para>
        /// <para>
        /// While many parameters are optional in the API, they must be provided (not <c>null</c>) to meet certification requirements. <see href="https://go.microsoft.com/fwlink/?linkid=2344479">Learn more about certification requirements</see>
        /// </para>
        /// <para>
        /// Either <paramref name="agentId"/> or <paramref name="agentPlatformId"/> should be provided to identify the agent.
        /// </para>
        /// </remarks>
        public AgentDetails(
            string? agentId = null,
            string? agentName = null,
            string? agentDescription = null,
            string? agenticUserId = null,
            string? agenticUserEmail = null,
            string? agentBlueprintId = null,
            string? tenantId = null,
            AgentType? agentType = null,
            IPAddress? agentClientIP = null,
            string? agentPlatformId = null,
            string? providerName = null,
            string? agentVersion = null)
        {
            AgentId = agentId;
            AgentName = agentName;
            AgentDescription = agentDescription;
            AgenticUserId = agenticUserId;
            AgenticUserEmail = agenticUserEmail;
            AgentBlueprintId = agentBlueprintId;
            TenantId = tenantId;
            AgentType = agentType;
            AgentClientIP = agentClientIP;
            AgentPlatformId = agentPlatformId;
            ProviderName = providerName;
            AgentVersion = agentVersion;
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
        /// Optional agentic user ID for the agent.
        /// </summary>
        public string? AgenticUserId { get; }

        /// <summary>
        /// Optional email address for the agentic user.
        /// </summary>
        public string? AgenticUserEmail { get; }

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
        /// Optional provider name (e.g., openai, anthropic).
        /// </summary>
        public string? ProviderName { get; }

        /// <summary>
        /// Optional version of the agent (e.g., "1.0.0", "2025-05-01").
        /// </summary>
        public string? AgentVersion { get; }

        /// <summary>
        /// Deconstructs the current instance into discrete values.
        /// </summary>
        /// <param name="agentId">Receives the agent identifier.</param>
        /// <param name="agentName">Receives the human-readable agent name.</param>
        /// <param name="agentDescription">Receives the agent description.</param>
        /// <param name="agenticUserId">Receives the agentic user ID.</param>
        /// <param name="agenticUserEmail">Receives the agentic user email.</param>
        /// <param name="agentBlueprintId">Receives the agent Blueprint/Application ID.</param>
        /// <param name="agentType">Receives the agent type.</param>
        /// <param name="tenantId">Receives the tenant identifier.</param>
        /// <param name="agentClientIP">Receives the client IP address.</param>
        /// <param name="agentPlatformId">Receives the platform ID.</param>
        /// <param name="agentVersion">Receives the agent version.</param>
        public void Deconstruct(
            out string? agentId,
            out string? agentName,
            out string? agentDescription,
            out string? agenticUserId,
            out string? agenticUserEmail,
            out string? agentBlueprintId,
            out AgentType? agentType,
            out string? tenantId,
            out IPAddress? agentClientIP,
            out string? agentPlatformId,
            out string? agentVersion)
        {
            agentId = AgentId;
            agentName = AgentName;
            agentDescription = AgentDescription;
            agenticUserId = AgenticUserId;
            agenticUserEmail = AgenticUserEmail;
            agentBlueprintId = AgentBlueprintId;
            agentType = AgentType;
            tenantId = TenantId;
            agentClientIP = AgentClientIP;
            agentPlatformId = AgentPlatformId;
            agentVersion = AgentVersion;
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
                   string.Equals(AgenticUserId, other.AgenticUserId, StringComparison.Ordinal) &&
                   string.Equals(AgenticUserEmail, other.AgenticUserEmail, StringComparison.Ordinal) &&
                   string.Equals(AgentBlueprintId, other.AgentBlueprintId, StringComparison.Ordinal) &&
                   AgentType == other.AgentType &&
                   string.Equals(TenantId, other.TenantId, StringComparison.Ordinal) &&
                   Equals(AgentClientIP, other.AgentClientIP) &&
                   string.Equals(AgentPlatformId, other.AgentPlatformId, StringComparison.Ordinal) &&
                   string.Equals(AgentVersion, other.AgentVersion, StringComparison.Ordinal);
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
                hash = (hash * 31) + (AgenticUserId != null ? StringComparer.Ordinal.GetHashCode(AgenticUserId) : 0);
                hash = (hash * 31) + (AgenticUserEmail != null ? StringComparer.Ordinal.GetHashCode(AgenticUserEmail) : 0);
                hash = (hash * 31) + (AgentBlueprintId != null ? StringComparer.Ordinal.GetHashCode(AgentBlueprintId) : 0);
                hash = (hash * 31) + (AgentType?.GetHashCode() ?? 0);
                hash = (hash * 31) + (TenantId != null ? StringComparer.Ordinal.GetHashCode(TenantId) : 0);
                hash = (hash * 31) + (AgentClientIP?.GetHashCode() ?? 0);
                hash = (hash * 31) + (AgentPlatformId != null ? StringComparer.Ordinal.GetHashCode(AgentPlatformId) : 0);
                hash = (hash * 31) + (AgentVersion != null ? StringComparer.Ordinal.GetHashCode(AgentVersion) : 0);
                return hash;
            }
        }
    }
}