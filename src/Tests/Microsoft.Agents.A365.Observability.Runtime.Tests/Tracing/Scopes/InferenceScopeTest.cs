// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;


[TestClass]
public sealed class InferenceScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_SetsExpectedTags()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai",
            123,
            456,
            new[] { "stop", "length" },
            "response-123");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, details.OperationName.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiRequestModelKey, details.Model);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiProviderNameKey, details.ProviderName);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageInputTokensKey, details.InputTokens!.Value.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageOutputTokensKey, details.OutputTokens!.Value.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", details.FinishReasons!));
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseIdKey, details.ResponseId!);
    }

    [TestMethod]
    public void RecordInputTokens_SetsTag()
    {
        var inputTokens = 789;
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordInputTokens(inputTokens);
        });
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageInputTokensKey, inputTokens.ToString());
    }

    [TestMethod]
    public void RecordOutputTokens_SetsTag()
    {
        var outputTokens = 321;
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordOutputTokens(outputTokens);
        });
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageOutputTokensKey, outputTokens.ToString());
    }

    [TestMethod]
    public void RecordResponseId_SetsTag()
    {
        var responseId = "resp-456";
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordResponseId(responseId);
        });
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseIdKey, responseId);
    }

    [TestMethod]
    public void RecordFinishReasons_SetsTag()
    {
        var finishReasons = new[] { "tool_calls", "stop" };
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");
        
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordFinishReasons(finishReasons);
        });
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", finishReasons));
    }

    [TestMethod]
    public void RecordInputMessages_SetsTag()
    {
        var messages = new[] { "Hello", "How are you?" };
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordInputMessages(messages);
        });

        activity.ShouldHaveTag("gen_ai.input.messages", string.Join(",", messages));
    }

    [TestMethod]
    public void RecordOutputMessages_SetsTag()
    {
        var messages = new[] { "Hi there!", "I'm fine." };
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordOutputMessages(messages);
        });

        activity.ShouldHaveTag("gen_ai.output.messages", string.Join(",", messages));
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails());
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }
}
