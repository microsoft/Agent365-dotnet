// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    /// Represents channel information for agent execution context.
    /// </summary>
    public sealed class Channel : IEquatable<Channel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Channel"/> class.
        /// </summary>
        /// <param name="name">Human-readable name of the channel.</param>
        /// <param name="link">Optional link for the channel.</param>
        public Channel(string? name, string? link = null)
        {
            Name = name;
            Link = link;
        }

        /// <summary>
        /// Gets the human-readable name for the channel.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Gets an optional link for the channel.
        /// </summary>
        public string? Link { get; }

        /// <summary>
        /// Deconstructs this instance for tuple deconstruction support.
        /// </summary>
        /// <param name="name">Receives the channel name.</param>
        /// <param name="link">Receives the link.</param>
        public void Deconstruct(out string? name, out string? link)
        {
            name = Name;
            link = Link;
        }

        /// <inheritdoc/>
        public bool Equals(Channel? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
                   string.Equals(Link, other.Link, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as Channel);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0);
                hash = (hash * 31) + (Link != null ? StringComparer.Ordinal.GetHashCode(Link) : 0);
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
        /// <param name="channel">Optional channel information describing request origin.</param>
        public Request(string content, ExecutionType? executionType = null, string? sessionId = null, Channel? channel = null)
        {
            Content = content;
            ExecutionType = executionType ?? Contracts.ExecutionType.Unknown;
            SessionId = sessionId;
            Channel = channel;
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
        /// Gets channel information describing the origin of the request.
        /// </summary>
        public Channel? Channel { get; }

        /// <summary>
        /// Deconstructs the request for tuple deconstruction support.
        /// </summary>
        /// <param name="content">Receives the request content.</param>
        /// <param name="executionType">Receives the execution type.</param>
        /// <param name="sessionId">Receives the session identifier.</param>
        /// <param name="channel">Receives the channel information.</param>
        public void Deconstruct(out string content, out ExecutionType? executionType, out string? sessionId, out Channel? channel)
        {
            content = Content;
            executionType = ExecutionType;
            sessionId = SessionId;
            channel = Channel;
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
                   EqualityComparer<Channel?>.Default.Equals(Channel, other.Channel);
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
                hash = (hash * 31) + EqualityComparer<Channel?>.Default.GetHashCode(Channel);
                return hash;
            }
        }
    }
}