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
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails());
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, details.OperationName.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiRequestModelKey, details.Model);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiProviderNameKey, details.ProviderName);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageInputTokensKey, details.InputTokens!.Value.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiUsageOutputTokensKey, details.OutputTokens!.Value.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiResponseFinishReasonsKey, string.Join(",", details.FinishReasons!));
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
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                parentContext: null,
                conversationId: conversationId,
                channel: null);
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
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                parentContext: null,
                conversationId: null,
                channel: metadata);
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
            using var scope = InferenceScope.Start(details, Util.GetAgentDetails(), Util.GetTenantDetails())!;
            scope.RecordThoughtProcess(thoughtProcess);
        });
        
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentThoughtProcessKey, thoughtProcess);
    }

    [TestMethod]
    public void Start_SetsCallerDetails_WhenProvided()
    {
        // Arrange
        var callerDetails = new CallerDetails(
            callerId: "caller-inf-123",
            callerName: "Inference Caller",
            callerUpn: "caller-inf@example.com",
            callerClientIP: System.Net.IPAddress.Parse("10.0.0.1"),
            tenantId: "tenant-inf-456");
        var details = new InferenceCallDetails(
            InferenceOperationType.Chat,
            "gpt-4o",
            "openai");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InferenceScope.Start(
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                callerDetails: callerDetails);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.UserIdKey, callerDetails.CallerId);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserNameKey, callerDetails.CallerName);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserEmailKey, callerDetails.CallerUpn);
        activity.ShouldHaveTag(OpenTelemetryConstants.CallerClientIpKey, callerDetails.CallerClientIP!.ToString());
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
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                startTime: customStartTime);
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
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                startTime: customStartTime,
                endTime: customEndTime);
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
                details,
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                startTime: customStartTime,
                endTime: initialEndTime);
            scope.SetEndTime(laterEndTime);
        });

        // Assert - The start time should be set
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }
}
