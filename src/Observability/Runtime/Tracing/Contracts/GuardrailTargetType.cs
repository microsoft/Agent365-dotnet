// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Well-known values for the type of content or action a guardrail is applied to.
    /// </summary>
    /// <remarks>
    /// This is a free-form field per the OpenTelemetry semantic conventions.
    /// These static members provide discoverability for common values, but custom strings
    /// are accepted via implicit conversion (e.g. <c>GuardrailTargetType myType = "custom_target";</c>).
    /// </remarks>
    public readonly struct GuardrailTargetType : IEquatable<GuardrailTargetType>
    {
        /// <summary>
        /// Input to a language model.
        /// </summary>
        public static readonly GuardrailTargetType LlmInput = new GuardrailTargetType("llm_input");

        /// <summary>
        /// Output from a language model.
        /// </summary>
        public static readonly GuardrailTargetType LlmOutput = new GuardrailTargetType("llm_output");

        /// <summary>
        /// A tool call action.
        /// </summary>
        public static readonly GuardrailTargetType ToolCall = new GuardrailTargetType("tool_call");

        /// <summary>
        /// A tool definition.
        /// </summary>
        public static readonly GuardrailTargetType ToolDefinition = new GuardrailTargetType("tool_definition");

        /// <summary>
        /// A memory store operation.
        /// </summary>
        public static readonly GuardrailTargetType MemoryStore = new GuardrailTargetType("memory_store");

        /// <summary>
        /// A memory retrieval operation.
        /// </summary>
        public static readonly GuardrailTargetType MemoryRetrieve = new GuardrailTargetType("memory_retrieve");

        /// <summary>
        /// A knowledge query.
        /// </summary>
        public static readonly GuardrailTargetType KnowledgeQuery = new GuardrailTargetType("knowledge_query");

        /// <summary>
        /// A knowledge retrieval result.
        /// </summary>
        public static readonly GuardrailTargetType KnowledgeResult = new GuardrailTargetType("knowledge_result");

        /// <summary>
        /// A message.
        /// </summary>
        public static readonly GuardrailTargetType Message = new GuardrailTargetType("message");

        /// <summary>
        /// Gets the string value of this target type.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuardrailTargetType"/> struct.
        /// </summary>
        /// <param name="value">The target type string value.</param>
        public GuardrailTargetType(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Implicitly converts a string to a <see cref="GuardrailTargetType"/>.
        /// </summary>
        public static implicit operator GuardrailTargetType(string value) => new GuardrailTargetType(value);

        /// <summary>
        /// Implicitly converts a <see cref="GuardrailTargetType"/> to its string value.
        /// </summary>
        public static implicit operator string(GuardrailTargetType type) => type.Value;

        /// <inheritdoc/>
        public bool Equals(GuardrailTargetType other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is GuardrailTargetType other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;

        /// <inheritdoc/>
        public override string ToString() => Value;

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(GuardrailTargetType left, GuardrailTargetType right) => left.Equals(right);

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(GuardrailTargetType left, GuardrailTargetType right) => !left.Equals(right);
    }
}
