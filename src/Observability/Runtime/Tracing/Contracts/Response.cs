// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a response from an AI agent with telemetry context.
    /// </summary>
    public sealed class Response : IEquatable<Response>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class.
        /// </summary>
        /// <param name="content">The payload content returned by the agent.</param>
        public Response(string content)
        {
            Content = content;
        }

        /// <summary>
        /// Gets the textual content of the response.
        /// </summary>
        public string Content { get; }

        /// <inheritdoc/>
        public bool Equals(Response? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Content, other.Content, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Response);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Content != null ? StringComparer.Ordinal.GetHashCode(Content) : 0;
        }
    }
}
