// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts.Messages
{
    /// <summary>
    /// Role of a message participant per OTEL gen-ai semantic conventions.
    /// </summary>
    public enum MessageRole
    {
        /// <summary>System message.</summary>
        System,

        /// <summary>User message.</summary>
        User,

        /// <summary>Assistant message.</summary>
        Assistant,

        /// <summary>Tool message.</summary>
        Tool,
    }

    /// <summary>
    /// Reason a model stopped generating per OTEL gen-ai semantic conventions.
    /// </summary>
    public enum FinishReason
    {
        /// <summary>Normal completion.</summary>
        Stop,

        /// <summary>Token limit reached.</summary>
        Length,

        /// <summary>Content filter triggered.</summary>
        ContentFilter,

        /// <summary>Tool call requested.</summary>
        ToolCall,

        /// <summary>Error occurred.</summary>
        Error,
    }

    /// <summary>
    /// Media modality for blob, file, and URI parts.
    /// </summary>
    public enum Modality
    {
        /// <summary>Image content.</summary>
        Image,

        /// <summary>Video content.</summary>
        Video,

        /// <summary>Audio content.</summary>
        Audio,
    }
}
