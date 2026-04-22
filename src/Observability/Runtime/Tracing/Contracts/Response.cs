// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents a response from an AI agent with output messages.
    /// Accepts plain strings (backward compat) or structured <see cref="OutputMessages"/>.
    /// </summary>
    public sealed class Response : IEquatable<Response>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class with plain string messages.
        /// </summary>
        /// <param name="messages">The output messages from the agent.</param>
        public Response(IReadOnlyList<string>? messages = null)
        {
            Messages = messages ?? Array.Empty<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class with structured output messages.
        /// </summary>
        /// <param name="outputContent">The structured output messages.</param>
        public Response(OutputMessages outputContent)
        {
            OutputContent = outputContent ?? throw new ArgumentNullException(nameof(outputContent));
            Messages = Array.Empty<string>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Response"/> class with a tool call result dictionary.
        /// Per OTEL spec, tool call results are expected to be objects and are serialized to JSON.
        /// </summary>
        /// <param name="toolResultObject">The tool call result as a structured dictionary.</param>
        public Response(IDictionary<string, object> toolResultObject)
        {
            ToolResultObject = toolResultObject ?? throw new ArgumentNullException(nameof(toolResultObject));
            Messages = Array.Empty<string>();
        }

        /// <summary>
        /// Gets the output messages from the agent response (backward compat).
        /// </summary>
        public IReadOnlyList<string> Messages { get; }

        /// <summary>
        /// Gets the structured output messages, when supplied.
        /// Takes precedence over <see cref="Messages"/> for telemetry recording.
        /// </summary>
        public OutputMessages? OutputContent { get; }

        /// <summary>
        /// Gets the tool call result dictionary, when supplied.
        /// Per OTEL spec, tool call results are serialized to JSON directly.
        /// </summary>
        public IDictionary<string, object>? ToolResultObject { get; }

        /// <inheritdoc/>
        public bool Equals(Response? other)
        {
            if (other is null)
            {
                return false;
            }

            if (!ReferenceEquals(OutputContent, other.OutputContent))
            {
                return false;
            }

            if (!ReferenceEquals(ToolResultObject, other.ToolResultObject))
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
                hash = (hash * 31) + (OutputContent != null ? OutputContent.GetHashCode() : 0);
                hash = (hash * 31) + (ToolResultObject != null ? ToolResultObject.GetHashCode() : 0);
                foreach (var message in Messages)
                {
                    hash = (hash * 31) + (message != null ? StringComparer.Ordinal.GetHashCode(message) : 0);
                }

                return hash;
            }
        }
    }
}
