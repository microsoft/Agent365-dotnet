// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents the details needed for invoking an AI agent with telemetry tracking.
    /// </summary>
    public sealed class InvokeAgentDetails : IEquatable<InvokeAgentDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeAgentDetails"/> class.
        /// </summary>
        /// <param name="details">Agent metadata for the invocation.</param>
        /// <param name="endpoint">Optional endpoint URI of the agent to invoke.</param>
        /// <param name="sessionId">Optional session identifier associated with the agent call.</param>
        public InvokeAgentDetails(AgentDetails details, Uri? endpoint = null, string? sessionId = null)
        {
            Endpoint = endpoint;
            Details = details;
            SessionId = sessionId;
        }

        /// <summary>
        /// The endpoint URI for the AI agent.
        /// </summary>
        public Uri? Endpoint { get; }

        /// <summary>
        /// Agent details associated with the invocation.
        /// </summary>
        public AgentDetails Details { get; }

        /// <summary>
        /// The session id associated with the invocation.
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// Deconstructs the invocation details for tuple deconstruction support.
        /// </summary>
        /// <param name="endpoint">Receives the endpoint URI.</param>
        /// <param name="details">Receives the agent details.</param>
        /// <param name="sessionId">Receives the session identifier.</param>
        public void Deconstruct(out Uri? endpoint, out AgentDetails details, out string? sessionId)
        {
            endpoint = Endpoint;
            details = Details;
            sessionId = SessionId;
        }

        /// <inheritdoc/>
        public bool Equals(InvokeAgentDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return EqualityComparer<Uri?>.Default.Equals(Endpoint, other.Endpoint) &&
                   EqualityComparer<AgentDetails>.Default.Equals(Details, other.Details) &&
                   string.Equals(SessionId, other.SessionId, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as InvokeAgentDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + EqualityComparer<Uri?>.Default.GetHashCode(Endpoint);
                hash = (hash * 31) + EqualityComparer<AgentDetails>.Default.GetHashCode(Details);
                hash = (hash * 31) + (SessionId != null ? StringComparer.Ordinal.GetHashCode(SessionId) : 0);
                return hash;
            }
        }
    }
}