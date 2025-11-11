// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Runtime.Serialization;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Supported inference operation types for generative AI.
    /// </summary>
    public enum InferenceOperationType
    {
        /// <summary>
        /// Chat-based inference operation.
        /// </summary>
        [EnumMember(Value = "chat")]
        Chat,
        
        /// <summary>
        /// Text completion inference operation.
        /// </summary>
        [EnumMember(Value = "text_completion")]
        TextCompletion,
        
        /// <summary>
        /// Content generation inference operation.
        /// </summary>
        [EnumMember(Value = "generate_content")]
        GenerateContent
    }
}