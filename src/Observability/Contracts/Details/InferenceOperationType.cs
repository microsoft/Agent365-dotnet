// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Contracts.Details
{
    /// <summary>
    /// Supported inference operation types for generative AI.
    /// </summary>
    public enum InferenceOperationType
    {
        /// <summary>
        /// Chat-based inference operation.
        /// </summary>
        Chat,
        
        /// <summary>
        /// Text completion inference operation.
        /// </summary>
        TextCompletion,
        
        /// <summary>
        /// Content generation inference operation.
        /// </summary>
        GenerateContent
    }
}