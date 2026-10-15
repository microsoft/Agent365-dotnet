// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Contracts;

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

[TestClass]
public sealed class InferenceCallDetailsTest
{
    [TestMethod]
    public void LegacySevenParameterConstructor_DefaultsNewFieldsToNull()
    {
        // Simulates a caller compiled against the original seven-parameter CLR
        // signature, to guard against reintroducing a binary-compatibility break.
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai",
            123,
            456,
            new[] { "stop" },
            "response-123");

        details.Model.Should().Be("gpt-4o");
        details.ResponseModel.Should().BeNull();
        details.CacheReadInputTokens.Should().BeNull();
        details.CacheWriteInputTokens.Should().BeNull();
        details.Endpoint.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_NormalizesWhitespaceOnlyModelToNull()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: "   ",
            providerName: "openai",
            inputTokens: null,
            outputTokens: null,
            finishReasons: null,
            responseId: null,
            responseModel: "gpt-4o-2024-08-06");

        details.Model.Should().BeNull();
        details.ResponseModel.Should().Be("gpt-4o-2024-08-06");
    }

    [TestMethod]
    public void Constructor_NormalizesWhitespaceOnlyResponseModelToNull()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: "gpt-4o",
            providerName: "openai",
            inputTokens: null,
            outputTokens: null,
            finishReasons: null,
            responseId: null,
            responseModel: "   ");

        details.Model.Should().Be("gpt-4o");
        details.ResponseModel.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_ThrowsWhenBothModelAndResponseModelAreBlank()
    {
        var act = () => new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: "   ",
            providerName: "openai",
            inputTokens: null,
            outputTokens: null,
            finishReasons: null,
            responseId: null,
            responseModel: string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
