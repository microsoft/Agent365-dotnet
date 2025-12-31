// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Represents different types of agent invocations in the system.
    /// </summary>
    public enum ExecutionType
    {
        /// <summary>
        /// Direct human-to-agent invocation (e.g., through UI, API call).
        /// </summary>
        HumanToAgent,
        
        /// <summary>
        /// Agent-to-agent invocation (e.g., one agent calling another).
        /// </summary>
        Agent2Agent,
        
        /// <summary>
        /// Event-driven agent invocation (e.g., scheduled, webhook, message queue).
        /// </summary>
        EventToAgent,

        /// <summary>
        /// Unknown or unspecified invocation type.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Represents different roles that can invoke an agent.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Role
    {
        /// <summary>
        /// Human user invoking the agent.
        /// </summary>
        Human,
        
        /// <summary>
        /// Another agent invoking this agent.
        /// </summary>
        Agent,
        
        /// <summary>
        /// Event-driven invocation (e.g., scheduled, webhook, message queue).
        /// </summary>
        Event,

        /// <summary>
        /// Unknown or unspecified role.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Represents metadata about the source (i.e. channel) of an invocation.
    /// </summary>
    public sealed class SourceMetadata : IEquatable<SourceMetadata>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourceMetadata"/> class.
        /// </summary>
        /// <param name="id">Unique identifier for the source.</param>
        /// <param name="name">Human-readable name of the source.</param>
        /// <param name="role">Optional role describing the source.</param>
        /// <param name="description">Optional description of the source.</param>
        public SourceMetadata(string? name, Role? role = null, string? description = null, string? id = null)
        {
            Id = id;
            Name = name;
            Role = role ?? Contracts.Role.Unknown;
            Description = description;
        }

        /// <summary>
        /// Gets the unique identifier for the source.
        /// </summary>
        public string? Id { get; }

        /// <summary>
        /// Gets the human-readable name for the source.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets the role associated with the source.
        /// </summary>
        public Role? Role { get; }

        /// <summary>
        /// Gets an optional description for the source.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Deconstructs this instance for tuple deconstruction support.
        /// </summary>
        /// <param name="id">Receives the source identifier.</param>
        /// <param name="name">Receives the source name.</param>
        /// <param name="role">Receives the role value.</param>
        /// <param name="description">Receives the description.</param>
        public void Deconstruct(out string? id, out string? name, out Role? role, out string? description)
        {
            id = Id;
            name = Name;
            role = Role;
            description = Description;
        }

        /// <inheritdoc/>
        public bool Equals(SourceMetadata? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Id, other.Id, StringComparison.Ordinal) &&
                   string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   Role == other.Role &&
                   string.Equals(Description, other.Description, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as SourceMetadata);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0);
                hash = (hash * 31) + (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0);
                hash = (hash * 31) + (Role?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Description != null ? StringComparer.Ordinal.GetHashCode(Description) : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Represents a request to an AI agent with telemetry context.
    /// </summary>
    public sealed class Request : IEquatable<Request>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Request"/> class.
        /// </summary>
        /// <param name="content">The payload content supplied to the agent.</param>
        /// <param name="executionType">Optional execution type describing the request.</param>
        /// <param name="sessionId">Optional session identifier.</param>
        /// <param name="sourceMetadata">Optional metadata describing request origin.</param>
        public Request(string content, ExecutionType? executionType = null, string? sessionId = null, SourceMetadata? sourceMetadata = null)
        {
            Content = content;
            ExecutionType = executionType ?? Contracts.ExecutionType.Unknown;
            SessionId = sessionId;
            SourceMetadata = sourceMetadata;
        }

        /// <summary>
        /// Gets the textual content of the request.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Gets the execution type associated with the request, if provided.
        /// </summary>
        public ExecutionType? ExecutionType { get; }

        /// <summary>
        /// Gets the session identifier, when supplied.
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// Gets metadata describing the origin (i.e. channel) of the request.
        /// </summary>
        public SourceMetadata? SourceMetadata { get; }

        /// <summary>
        /// Deconstructs the request for tuple deconstruction support.
        /// </summary>
        /// <param name="content">Receives the request content.</param>
        /// <param name="executionType">Receives the execution type.</param>
        /// <param name="sessionId">Receives the session identifier.</param>
        /// <param name="sourceMetadata">Receives the source metadata.</param>
        public void Deconstruct(out string content, out ExecutionType? executionType, out string? sessionId, out SourceMetadata? sourceMetadata)
        {
            content = Content;
            executionType = ExecutionType;
            sessionId = SessionId;
            sourceMetadata = SourceMetadata;
        }

        /// <inheritdoc/>
        public bool Equals(Request? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Content, other.Content, StringComparison.Ordinal) &&
                   ExecutionType == other.ExecutionType &&
                   string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
                   EqualityComparer<SourceMetadata?>.Default.Equals(SourceMetadata, other.SourceMetadata);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Request);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (Content != null ? StringComparer.Ordinal.GetHashCode(Content) : 0);
                hash = (hash * 31) + (ExecutionType?.GetHashCode() ?? 0);
                hash = (hash * 31) + (SessionId != null ? StringComparer.Ordinal.GetHashCode(SessionId) : 0);
                hash = (hash * 31) + EqualityComparer<SourceMetadata?>.Default.GetHashCode(SourceMetadata);
                return hash;
            }
        }
    }
}