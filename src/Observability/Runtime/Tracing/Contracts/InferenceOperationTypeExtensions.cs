// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Extension helpers for <see cref="InferenceOperationType"/>.
    /// </summary>
    public static class InferenceOperationTypeExtensions
    {
        /// <summary>
        /// Maps an <see cref="InferenceOperationType"/> to its canonical lowercase
        /// GenAI semantic-convention operation name (e.g. "chat", "text_completion", "generate_content").
        /// </summary>
        /// <param name="operationType">The operation type to map.</param>
        /// <returns>The canonical GenAI operation name.</returns>
        public static string ToCanonicalName(this InferenceOperationType operationType) => operationType switch
        {
            InferenceOperationType.Chat => "chat",
            InferenceOperationType.TextCompletion => "text_completion",
            InferenceOperationType.GenerateContent => "generate_content",
            _ => operationType.ToString(),
        };
    }
}
