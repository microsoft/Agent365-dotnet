// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, "chat");
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiRequestModelKey, details.Model!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiProviderNameKey, details.ProviderName);
        activity.GetTagItem(OpenTelemetryConstants.GenAiUsageInputTokensKey).Should().Be(details.InputTokens);
        activity.GetTagItem(OpenTelemetryConstants.GenAiUsageOutputTokensKey).Should().Be(details.OutputTokens);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", details.FinishReasons!));
    }

    [TestMethod]
    public void Start_SetsModelObservationSchemaAttributes()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: null,
            providerName: "openai",
            inputTokens: 123,
            outputTokens: 45,
            finishReasons: null,
            responseId: null,
            responseModel: "gpt-4o-2024-08-06",
            cacheReadInputTokens: 20,
            cacheWriteInputTokens: 10,
            endpoint: new Uri("https://models.example.com:8443/v1/chat"));
        var agent = new AgentDetails("agent-1", agentType: AgentType.Foundry);

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, agent);
        });

        activity.ShouldHaveTag(GenAiOperationNameKey, "chat");
        activity.GetTagItem(GenAiRequestModelKey).Should().BeNull();
        activity.ShouldHaveTag(GenAiResponseModelKey, "gpt-4o-2024-08-06");
        activity.ShouldHaveTag(GenAiProviderNameKey, "openai");
        activity.ShouldHaveTag(GenAiAgentIdKey, "agent-1");
        activity.ShouldHaveTag(GenAiAgentTypeKey, "Foundry");
        activity.GetTagItem(GenAiUsageInputTokensKey).Should().Be(123);
        activity.GetTagItem(GenAiUsageOutputTokensKey).Should().Be(45);
        activity.GetTagItem(GenAiUsageCacheReadInputTokensKey).Should().Be(20);
        activity.GetTagItem(GenAiUsageCacheWriteInputTokensKey).Should().Be(10);
        activity.ShouldHaveTag(ServerAddressKey, "models.example.com");
        activity.GetTagItem(ServerPortKey).Should().Be(8443);
    }

    [TestMethod]
    [DataRow(InferenceOperationType.Chat, "chat")]
    [DataRow(InferenceOperationType.TextCompletion, "text_completion")]
    [DataRow(InferenceOperationType.GenerateContent, "generate_content")]
    public void Start_UsesCanonicalOperationNameForEachOperationType(InferenceOperationType operationType, string expectedOperationName)
    {
        var details = new InferenceCallDetails(
            operationType,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, expectedOperationName);
        activity!.OperationName.Should().Be($"{expectedOperationName} gpt-4o");
    }

    [TestMethod]
    public void Constructor_RequiresModelIdentityAndProvider()
    {
        var missingModel = () => new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: null!,
            providerName: "openai");
        var missingProvider = () => new InferenceCallDetails(
            InferenceOperationType.Chat,
            model: "gpt-4o",
            providerName: "");

        missingModel.Should().Throw<ArgumentException>();
        missingProvider.Should().Throw<ArgumentException>();
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
            scope.RecordInputTokens(inputTokens);
        });
        activity.GetTagItem(OpenTelemetryConstants.GenAiUsageInputTokensKey).Should().Be(inputTokens);
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
            scope.RecordOutputTokens(outputTokens);
        });
        activity.GetTagItem(OpenTelemetryConstants.GenAiUsageOutputTokensKey).Should().Be(outputTokens);
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
            scope.RecordInputMessages(messages);
        });

        var tagValue = activity.Tags.First(t => t.Key == "gen_ai.input.messages").Value;
        tagValue.Should().StartWith("[");
        tagValue.Should().Contain("\"role\":\"user\"");
        tagValue.Should().Contain("Hello");
        tagValue.Should().Contain("How are you?");
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
            scope.RecordOutputMessages(messages);
        });

        var tagValue = activity.Tags.First(t => t.Key == "gen_ai.output.messages").Value;
        tagValue.Should().StartWith("[");
        tagValue.Should().Contain("\"role\":\"assistant\"");
        tagValue.Should().Contain("Hi there!");
        tagValue.Should().Contain("I\\u0027m fine.");
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
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails());
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_SetsConversationId_WhenProvided()
    {
        var conversationId = "conv-inf-123";
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                new Request(conversationId: conversationId),
                details,
                Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
    }

    [TestMethod]
    public void Start_SetsChannel_Tags()
    {
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");
        var metadata = new Channel(name: "ChannelZ", link: "https://channel/link/z");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                new Request(channel: metadata),
                details,
                Util.GetAgentDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.ChannelNameKey, metadata.Name!);
        activity.ShouldHaveTag(OpenTelemetryConstants.ChannelLinkKey, metadata.Link!);
    }

    [TestMethod]
    public void RecordThoughtProcess_SetsTag()
    {
        var thoughtProcess = "First, I analyzed the user's request. Then, I determined the best approach would be to provide a structured response. Finally, I composed the answer.";
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(Util.GetDefaultRequest(), details, Util.GetAgentDetails())!;
            scope.RecordThoughtProcess(thoughtProcess);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentThoughtProcessKey, thoughtProcess);
    }

    [TestMethod]
    public void Start_SetsCallerDetails_WhenProvided()
    {
        // Arrange
        var userDetails = new UserDetails(
            userId: "caller-inf-123",
            userName: "Inference Caller",
            userEmail: "caller-inf@example.com",
            userClientIP: System.Net.IPAddress.Parse("10.0.0.1"));
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                details,
                Util.GetAgentDetails(),
                userDetails: userDetails);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.UserIdKey, userDetails.UserId!);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserNameKey, userDetails.UserName!);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserEmailKey, userDetails.UserEmail!);
        activity.ShouldHaveTag(OpenTelemetryConstants.CallerClientIpKey, userDetails.UserClientIP!.ToString());
    }

    [TestMethod]
    public void Start_WithCustomStartTime_SetsActivityStartTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                details,
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(startTime: customStartTime));
        });

        // Assert
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_WithCustomStartAndEndTime_SetsActivityTimes()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);
        var customEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 25, TimeSpan.Zero); // 5 seconds later
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                details,
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: customEndTime));
        });

        // Assert - Start time should be set to custom time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void SetEndTime_OverridesEndTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 40, TimeSpan.Zero);
        var initialEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 45, TimeSpan.Zero);
        var laterEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 48, TimeSpan.Zero);
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                Util.GetDefaultRequest(),
                details,
                Util.GetAgentDetails(),
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: initialEndTime));
            scope.SetEndTime(laterEndTime);
        });

        // Assert - The start time should be set
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }
}
