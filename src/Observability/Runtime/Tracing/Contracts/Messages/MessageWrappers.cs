// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages
{
    /// <summary>
    /// Schema version embedded in serialized message payloads.
    /// </summary>
    public static class MessageConstants
    {
        /// <summary>
        /// The current schema version for structured message payloads.
        /// </summary>
        public const string SchemaVersion = "0.1.0";
    }

    /// <summary>
    /// Versioned wrapper for input messages.
    /// </summary>
    public sealed class InputMessages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputMessages"/> class.
        /// </summary>
        /// <param name="messages">The input chat messages.</param>
        public InputMessages(IReadOnlyList<ChatMessage> messages)
        {
            Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        }

        /// <summary>Gets the input chat messages.</summary>
        public IReadOnlyList<ChatMessage> Messages { get; }

        /// <summary>Gets the schema version.</summary>
        public string Version => MessageConstants.SchemaVersion;
    }

    /// <summary>
    /// Versioned wrapper for output messages.
    /// </summary>
    public sealed class OutputMessages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputMessages"/> class.
        /// </summary>
        /// <param name="messages">The output messages.</param>
        public OutputMessages(IReadOnlyList<OutputMessage> messages)
        {
            Messages = messages ?? throw new ArgumentNullException(nameof(messages));
        }

        /// <summary>Gets the output messages.</summary>
        public IReadOnlyList<OutputMessage> Messages { get; }

        /// <summary>Gets the schema version.</summary>
        public string Version => MessageConstants.SchemaVersion;
    }
}
