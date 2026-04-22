// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages
{
    /// <summary>
    /// An input message sent to a model (OTEL gen-ai semantic conventions).
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChatMessage"/> class.
        /// </summary>
        /// <param name="role">The role of the message sender.</param>
        /// <param name="parts">The message parts.</param>
        /// <param name="name">Optional participant name.</param>
        public ChatMessage(MessageRole role, IReadOnlyList<IMessagePart> parts, string? name = null)
        {
            Role = role;
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            Name = name;
        }

        /// <summary>Gets the role of the message sender.</summary>
        public MessageRole Role { get; }

        /// <summary>Gets the message parts.</summary>
        public IReadOnlyList<IMessagePart> Parts { get; }

        /// <summary>Gets the optional participant name.</summary>
        public string? Name { get; }
    }

    /// <summary>
    /// An output message produced by a model (OTEL gen-ai semantic conventions).
    /// </summary>
    public class OutputMessage : ChatMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputMessage"/> class.
        /// </summary>
        /// <param name="role">The role of the message sender.</param>
        /// <param name="parts">The message parts.</param>
        /// <param name="name">Optional participant name.</param>
        /// <param name="finishReason">Optional reason the model stopped generating.</param>
        public OutputMessage(MessageRole role, IReadOnlyList<IMessagePart> parts, string? name = null, string? finishReason = null)
            : base(role, parts, name)
        {
            FinishReason = finishReason;
        }

        /// <summary>Gets the optional reason the model stopped generating.</summary>
        public string? FinishReason { get; }
    }

}
