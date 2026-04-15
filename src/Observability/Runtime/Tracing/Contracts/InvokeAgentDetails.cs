// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Scope-level configuration for agent invocation tracing.
    /// </summary>
    public sealed class InvokeAgentScopeDetails : IEquatable<InvokeAgentScopeDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeAgentScopeDetails"/> class.
        /// </summary>
        /// <param name="endpoint">Optional endpoint URI of the agent to invoke.</param>
        public InvokeAgentScopeDetails(Uri? endpoint = null)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// The endpoint URI for the AI agent.
        /// </summary>
        public Uri? Endpoint { get; }

        /// <inheritdoc/>
        public bool Equals(InvokeAgentScopeDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return EqualityComparer<Uri?>.Default.Equals(Endpoint, other.Endpoint);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as InvokeAgentScopeDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return EqualityComparer<Uri?>.Default.GetHashCode(Endpoint);
        }
    }
}