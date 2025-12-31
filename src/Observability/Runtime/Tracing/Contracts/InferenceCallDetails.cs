// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts
{
    /// <summary>
    /// Details of an inference call for generative AI operations.
    /// </summary>
    public sealed class InferenceCallDetails : IEquatable<InferenceCallDetails>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InferenceCallDetails"/> class.
        /// </summary>
        /// <param name="operationName">Telemetry identifier for the inference operation.</param>
        /// <param name="model">Model name used to satisfy the inference request.</param>
        /// <param name="providerName">Provider responsible for the inference call.</param>
        /// <param name="inputTokens">Optional count of tokens provided as input.</param>
        /// <param name="outputTokens">Optional count of tokens produced by the model.</param>
        /// <param name="finishReasons">Optional set of finish reasons supplied by the model.</param>
        /// <param name="responseId">Optional identifier for the model response.</param>
        public InferenceCallDetails(
            InferenceOperationType operationName,
            string model,
            string providerName,
            int? inputTokens = null,
            int? outputTokens = null,
            string[]? finishReasons = null,
            string? responseId = null)
        {
            OperationName = operationName;
            Model = model;
            ProviderName = providerName;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            FinishReasons = finishReasons;
            ResponseId = responseId;
        }

        /// <summary>
        /// Gets the operation name associated with the inference call.
        /// </summary>
        public InferenceOperationType OperationName { get; }

        /// <summary>
        /// Gets the language model name used by the call.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Gets the provider responsible for servicing the inference request.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Gets the number of input tokens, when available.
        /// </summary>
        public int? InputTokens { get; }

        /// <summary>
        /// Gets the number of output tokens, when available.
        /// </summary>
        public int? OutputTokens { get; }

        /// <summary>
        /// Gets the model-provided finish reasons, when provided.
        /// </summary>
        public string[]? FinishReasons { get; }

        /// <summary>
        /// Gets the identifier associated with the model's response payload.
        /// </summary>
        public string? ResponseId { get; }

        /// <summary>
        /// Deconstructs this instance into individual components.
        /// </summary>
        /// <param name="operationName">Receives the operation name.</param>
        /// <param name="model">Receives the model name.</param>
        /// <param name="providerName">Receives the provider name.</param>
        /// <param name="inputTokens">Receives the input token count.</param>
        /// <param name="outputTokens">Receives the output token count.</param>
        /// <param name="finishReasons">Receives the finish reasons.</param>
        /// <param name="responseId">Receives the response identifier.</param>
        public void Deconstruct(
            out InferenceOperationType operationName,
            out string model,
            out string providerName,
            out int? inputTokens,
            out int? outputTokens,
            out string[]? finishReasons,
            out string? responseId)
        {
            operationName = OperationName;
            model = Model;
            providerName = ProviderName;
            inputTokens = InputTokens;
            outputTokens = OutputTokens;
            finishReasons = FinishReasons;
            responseId = ResponseId;
        }

        /// <inheritdoc/>
        public bool Equals(InferenceCallDetails? other)
        {
            if (other is null)
            {
                return false;
            }

            return OperationName == other.OperationName &&
                   string.Equals(Model, other.Model, StringComparison.Ordinal) &&
                   string.Equals(ProviderName, other.ProviderName, StringComparison.Ordinal) &&
                   InputTokens == other.InputTokens &&
                   OutputTokens == other.OutputTokens &&
                   Equals(FinishReasons, other.FinishReasons) &&
                   string.Equals(ResponseId, other.ResponseId, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as InferenceCallDetails);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + OperationName.GetHashCode();
                hash = (hash * 31) + (Model != null ? StringComparer.Ordinal.GetHashCode(Model) : 0);
                hash = (hash * 31) + (ProviderName != null ? StringComparer.Ordinal.GetHashCode(ProviderName) : 0);
                hash = (hash * 31) + (InputTokens?.GetHashCode() ?? 0);
                hash = (hash * 31) + (OutputTokens?.GetHashCode() ?? 0);
                hash = (hash * 31) + EqualityComparer<string[]?>.Default.GetHashCode(FinishReasons);
                hash = (hash * 31) + (ResponseId != null ? StringComparer.Ordinal.GetHashCode(ResponseId) : 0);
                return hash;
            }
        }
    }
}