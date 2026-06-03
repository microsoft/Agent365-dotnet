// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
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
        /// <param name="sessionId">Optional session identifier.</param>
        /// <param name="channel">Optional channel information describing request origin.</param>
        /// <param name="conversationId">Optional conversation or session correlation ID.</param>
        /// <param name="operationSource">Optional source of the operation (e.g., SDK, Gateway, MCPServer).</param>
        public Request(string? content = null, string? sessionId = null, Channel? channel = null, string? conversationId = null, string? operationSource = null)
        {
            Content = content;
            SessionId = sessionId;
            Channel = channel;
            ConversationId = conversationId;
            OperationSource = operationSource;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Request"/> class with structured input content.
        /// </summary>
        /// <param name="inputContent">The structured input messages for the agent.</param>
        /// <param name="sessionId">Optional session identifier.</param>
        /// <param name="channel">Optional channel information describing request origin.</param>
        /// <param name="conversationId">Optional conversation or session correlation ID.</param>
        /// <param name="operationSource">Optional source of the operation (e.g., SDK, Gateway, MCPServer).</param>
        public Request(InputMessages inputContent, string? sessionId = null, Channel? channel = null, string? conversationId = null, string? operationSource = null)
        {
            InputContent = inputContent ?? throw new ArgumentNullException(nameof(inputContent));
            SessionId = sessionId;
            Channel = channel;
            ConversationId = conversationId;
            OperationSource = operationSource;
        }

        /// <summary>
        /// Gets the textual content of the request.
        /// </summary>
        public string? Content { get; }

        /// <summary>
        /// Gets the structured input messages, when supplied.
        /// Takes precedence over <see cref="Content"/> for telemetry recording.
        /// </summary>
        public InputMessages? InputContent { get; }

        /// <summary>
        /// Gets the session identifier, when supplied.
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// Gets channel information describing the origin of the request.
        /// </summary>
        public Channel? Channel { get; }

        /// <summary>
        /// Gets the conversation or session correlation ID.
        /// </summary>
        public string? ConversationId { get; }

        /// <summary>
        /// Gets the source of the operation, when supplied.
        /// </summary>
        public string? OperationSource { get; }

        /// <summary>
        /// Deconstructs the request for tuple deconstruction support.
        /// </summary>
        /// <param name="content">Receives the request content.</param>
        /// <param name="sessionId">Receives the session identifier.</param>
        /// <param name="channel">Receives the channel information.</param>
        /// <param name="conversationId">Receives the conversation ID.</param>
        public void Deconstruct(out string? content, out string? sessionId, out Channel? channel, out string? conversationId)
        {
            content = Content;
            sessionId = SessionId;
            channel = Channel;
            conversationId = ConversationId;
        }

        /// <inheritdoc/>
        public bool Equals(Request? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(Content, other.Content, StringComparison.Ordinal) &&
                   string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
                   EqualityComparer<Channel?>.Default.Equals(Channel, other.Channel) &&
                   string.Equals(ConversationId, other.ConversationId, StringComparison.Ordinal) &&
                   string.Equals(OperationSource, other.OperationSource, StringComparison.Ordinal) &&
                   ReferenceEquals(InputContent, other.InputContent);
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
                hash = (hash * 31) + (SessionId != null ? StringComparer.Ordinal.GetHashCode(SessionId) : 0);
                hash = (hash * 31) + EqualityComparer<Channel?>.Default.GetHashCode(Channel);
                hash = (hash * 31) + (ConversationId != null ? StringComparer.Ordinal.GetHashCode(ConversationId) : 0);
                hash = (hash * 31) + (OperationSource != null ? StringComparer.Ordinal.GetHashCode(OperationSource) : 0);
                hash = (hash * 31) + (InputContent != null ? InputContent.GetHashCode() : 0);
                return hash;
            }
        }
    }
}