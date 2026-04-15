// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

[TestClass]
public sealed class OutputScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_SetsExpectedTags()
    {
        // Arrange
        var initialMessages = new[] { "Hello", "World" };
        var response = new Response(initialMessages);
        var agentDetails = new AgentDetails(
            agentId: "agent-output-123",
            agentName: "OutputAgent",
            agentType: AgentType.MicrosoftCopilot);

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetDefaultRequest(), response, agentDetails);
        });

        // Assert - operation name and activity name
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, OutputScope.OperationName);
        activity.DisplayName.Should().Be($"{OutputScope.OperationName} {agentDetails.AgentId}");

        // Assert - agent details
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName!);

        // Assert - output messages (structured JSON format)
        var tagValue = activity.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"assistant\"");
        tagValue.Should().Contain("Hello");
        tagValue.Should().Contain("World");
    }

    [TestMethod]
    public void RecordOutputMessages_OverwritesMessages()
    {
        // Arrange
        var initialMessages = new[] { "Hello", "World" };
        var additionalMessages = new[] { "Goodbye", "Moon" };
        var response = new Response(initialMessages);
        var agentDetails = Util.GetAgentDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetDefaultRequest(), response, agentDetails);
            scope.RecordOutputMessages(additionalMessages);
        });

        // Assert - output messages are overwritten (set-once), only additional messages remain
        var tagValue = activity.Tags.First(t => t.Key == OpenTelemetryConstants.GenAiOutputMessagesKey).Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"assistant\"");
        tagValue.Should().Contain("Goodbye");
        tagValue.Should().Contain("Moon");
    }

    [TestMethod]
    public void Start_WithParentContext_SetsParentCorrectly()
    {
        // Arrange
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();

        // Create a parent activity to get a valid parent context
        ActivityContext? parentContext = null;
        ListenForActivity(() =>
        {
            using var parentScope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            parentContext = parentScope.GetActivityContext();
        });

        // Act
        var childActivity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                response,
                agentDetails,
                spanDetails: new SpanDetails(parentContext: parentContext));
        });

        // Assert - child activity should have the parent set
        childActivity.ParentSpanId.ToString().Should().NotBeNullOrEmpty();
        childActivity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, OutputScope.OperationName);
        childActivity.ShouldHaveTagContaining(OpenTelemetryConstants.GenAiOutputMessagesKey, "Test message");
    }

    [TestMethod]
    public void Start_WithCustomStartTime_SetsActivityStartTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                response,
                agentDetails,
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
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                response,
                agentDetails,
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: customEndTime));
        });

        // Assert - Start time should be set to custom time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_SetsConversationIdChannelAndCallerDetails_WhenProvided()
    {
        // Arrange
        var conversationId = "conv-output-123";
        var metadata = new Channel(name: "ChannelOutput", link: "https://channel/output");
        var userDetails = new UserDetails(
            userId: "caller-output-123",
            userName: "Output Caller",
            userEmail: "caller-output@example.com",
            userClientIP: System.Net.IPAddress.Parse("10.0.0.2"));
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                new Request(conversationId: conversationId, channel: metadata),
                response,
                agentDetails,
                userDetails: userDetails);
        });

        // Assert - conversation ID
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);

        // Assert - source metadata
        activity.ShouldHaveTag(OpenTelemetryConstants.ChannelNameKey, metadata.Name!);
        activity.ShouldHaveTag(OpenTelemetryConstants.ChannelLinkKey, metadata.Link!);

        // Assert - caller details
        activity.ShouldHaveTag(OpenTelemetryConstants.UserIdKey, userDetails.UserId!);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserNameKey, userDetails.UserName!);
        activity.ShouldHaveTag(OpenTelemetryConstants.UserEmailKey, userDetails.UserEmail!);
        activity.ShouldHaveTag(OpenTelemetryConstants.CallerClientIpKey, userDetails.UserClientIP!.ToString());
    }

    [TestMethod]
    public void SetEndTime_OverridesEndTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 40, TimeSpan.Zero);
        var initialEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 45, TimeSpan.Zero);
        var laterEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 48, TimeSpan.Zero);
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetDefaultRequest(),
                response,
                agentDetails,
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: initialEndTime));
            scope.SetEndTime(laterEndTime);
        });

        // Assert - The start time should be set
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }
}
