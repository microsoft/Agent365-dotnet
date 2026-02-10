// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a response from an AI agent with output messages.
    /// </summary>
    public sealed class Response : IEquatable<Response>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class.
        /// </summary>
        /// <param name="messages">The output messages from the agent.</param>
        public Response(IReadOnlyList<string>? messages = null)
        {
            Messages = messages ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets the output messages from the agent response.
        /// </summary>
        public IReadOnlyList<string> Messages { get; }

        /// <inheritdoc/>
        public bool Equals(Response? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Messages.Count != other.Messages.Count)
            {
                return false;
            }

            for (int i = 0; i < Messages.Count; i++)
            {
                if (!string.Equals(Messages[i], other.Messages[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Response);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var message in Messages)
                {
                    hash = (hash * 31) + (message != null ? StringComparer.Ordinal.GetHashCode(message) : 0);
                }

                return hash;
            }
        }
    }
}
