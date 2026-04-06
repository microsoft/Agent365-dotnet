// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Details of a tool call made by an agent in the system.
    /// </summary>
    public sealed class ToolCallDetails : IEquatable<ToolCallDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallDetails"/> class.
        /// </summary>
        /// <param name="toolName">Name of the tool being invoked.</param>
        /// <param name="arguments">Optional serialized arguments passed to the tool.</param>
        /// <param name="toolCallId">Optional identifier for the tool invocation.</param>
        /// <param name="description">Optional description of the tool call.</param>
        /// <param name="toolType">Optional type classification for the tool.</param>
        /// <param name="endpoint">Optional endpoint for remote tool execution.</param>
        /// <param name="toolServerName">Optional server name for the tool.</param>
        public ToolCallDetails(
            string toolName,
            string? arguments,
            string? toolCallId = null,
            string? description = null,
            string? toolType = null,
            Uri? endpoint = null,
            string? toolServerName = null)
        {
            ToolName = toolName;
            Arguments = arguments;
            ToolCallId = toolCallId;
            Description = description;
            ToolType = toolType;
            Endpoint = endpoint;
            ToolServerName = toolServerName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallDetails"/> class with structured arguments.
        /// Per OTEL spec, tool arguments are expected to be an object and SHOULD be recorded in structured form.
        /// The dictionary is serialized to JSON when setting the span attribute.
        /// </summary>
        /// <param name="toolName">Name of the tool being invoked.</param>
        /// <param name="argumentsObject">Structured arguments passed to the tool, serialized to JSON.</param>
        /// <param name="toolCallId">Optional identifier for the tool invocation.</param>
        /// <param name="description">Optional description of the tool call.</param>
        /// <param name="toolType">Optional type classification for the tool.</param>
        /// <param name="endpoint">Optional endpoint for remote tool execution.</param>
        /// <param name="toolServerName">Optional server name for the tool.</param>
        public ToolCallDetails(
            string toolName,
            IDictionary<string, object> argumentsObject,
            string? toolCallId = null,
            string? description = null,
            string? toolType = null,
            Uri? endpoint = null,
            string? toolServerName = null)
        {
            ToolName = toolName;
            ArgumentsObject = argumentsObject ?? throw new ArgumentNullException(nameof(argumentsObject));
            ToolCallId = toolCallId;
            Description = description;
            ToolType = toolType;
            Endpoint = endpoint;
            ToolServerName = toolServerName;
        }

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public string ToolName { get; }

        /// <summary>
        /// Gets the serialized JSON arguments supplied to the tool, when any.
        /// </summary>
        public string? Arguments { get; }

        /// <summary>
        /// Gets the structured arguments supplied to the tool, when any.
        /// Takes precedence over <see cref="Arguments"/> for telemetry recording.
        /// </summary>
        public IDictionary<string, object>? ArgumentsObject { get; }

        /// <summary>
        /// Gets the identifier for the tool call, when provided.
        /// </summary>
        public string? ToolCallId { get; }

        /// <summary>
        /// Gets a user-facing description of the tool call, when provided.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the classification for the tool, when provided.
        /// </summary>
        public string? ToolType { get; }

        /// <summary>
        /// Gets the endpoint associated with the tool, when applicable.
        /// </summary>
        public Uri? Endpoint { get; }

        /// <summary>
        /// Gets the server name associated with the tool, when provided.
        /// </summary>
        public string? ToolServerName { get; }

        /// <summary>
        /// Deconstructs this instance into individual tool call components.
        /// </summary>
        /// <param name="toolName">Receives the tool name.</param>
        /// <param name="arguments">Receives the arguments payload.</param>
        /// <param name="toolCallId">Receives the tool call identifier.</param>
        /// <param name="description">Receives the human-readable description.</param>
        /// <param name="toolType">Receives the type hint.</param>
        /// <param name="endpoint">Receives the endpoint.</param>
        /// <param name="toolServerName">Receives the tool server name.</param>
        public void Deconstruct(out string toolName, out string? arguments, out string? toolCallId, out string? description, out string? toolType, out Uri? endpoint, out string? toolServerName)
        {
            toolName = ToolName;
            arguments = Arguments;
            toolCallId = ToolCallId;
            description = Description;
            toolType = ToolType;
            endpoint = Endpoint;
            toolServerName = ToolServerName;
        }

        /// <inheritdoc/>
        public bool Equals(ToolCallDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(ToolName, other.ToolName, StringComparison.Ordinal) &&
                   string.Equals(Arguments, other.Arguments, StringComparison.Ordinal) &&
                   string.Equals(ToolCallId, other.ToolCallId, StringComparison.Ordinal) &&
                   string.Equals(Description, other.Description, StringComparison.Ordinal) &&
                   string.Equals(ToolType, other.ToolType, StringComparison.Ordinal) &&
                   EqualityComparer<Uri?>.Default.Equals(Endpoint, other.Endpoint) &&
                   string.Equals(ToolServerName, other.ToolServerName, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as ToolCallDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (ToolName != null ? StringComparer.Ordinal.GetHashCode(ToolName) : 0);
                hash = (hash * 31) + (Arguments != null ? StringComparer.Ordinal.GetHashCode(Arguments) : 0);
                hash = (hash * 31) + (ToolCallId != null ? StringComparer.Ordinal.GetHashCode(ToolCallId) : 0);
                hash = (hash * 31) + (Description != null ? StringComparer.Ordinal.GetHashCode(Description) : 0);
                hash = (hash * 31) + (ToolType != null ? StringComparer.Ordinal.GetHashCode(ToolType) : 0);
                hash = (hash * 31) + EqualityComparer<Uri?>.Default.GetHashCode(Endpoint);
                hash = (hash * 31) + (ToolServerName != null ? StringComparer.Ordinal.GetHashCode(ToolServerName) : 0);
                return hash;
            }
        }
    }
}