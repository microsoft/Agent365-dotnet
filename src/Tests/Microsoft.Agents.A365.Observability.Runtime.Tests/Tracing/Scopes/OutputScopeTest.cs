// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

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
        var tenantDetails = new TenantDetails(Guid.NewGuid());

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, tenantDetails, response);
        });

        // Assert - operation name and activity name
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, OutputScope.OperationName);
        activity.DisplayName.Should().Be($"{OutputScope.OperationName} {agentDetails.AgentId}");

        // Assert - agent details
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentIdKey, agentDetails.AgentId!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentNameKey, agentDetails.AgentName!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentTypeKey, agentDetails.AgentType!.ToString()!);

        // Assert - output messages
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOutputMessagesKey, string.Join(",", initialMessages));
    }

    [TestMethod]
    public void RecordOutputMessages_AppendsMessages()
    {
        // Arrange
        var initialMessages = new[] { "Hello", "World" };
        var additionalMessages = new[] { "Goodbye", "Moon" };
        var response = new Response(initialMessages);
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, tenantDetails, response);
            scope.RecordOutputMessages(additionalMessages);
        });

        // Assert - output messages are appended (initial + additional)
        var expectedMessages = string.Join(",", initialMessages) + "," + string.Join(",", additionalMessages);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiOutputMessagesKey, expectedMessages);
    }

    [TestMethod]
    public void Start_WithParentId_SetsParentIdCorrectly()
    {
        // Arrange
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Create a parent activity to get a valid parent ID
        string? parentId = null;
        ListenForActivity(() =>
        {
            using var parentScope = InvokeAgentScope.Start(Details, tenantDetails);
            parentId = parentScope.Id;
        });

        // Act
        var childActivity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, tenantDetails, response, parentId: parentId);
        });

        // Assert - child activity should have the parent set
        childActivity.ParentId.Should().Be(parentId);
        childActivity.ShouldHaveTag(OpenTelemetryConstants.GenAiOperationNameKey, OutputScope.OperationName);
        childActivity.ShouldHaveTag(OpenTelemetryConstants.GenAiOutputMessagesKey, "Test message");
    }

    [TestMethod]
    public void Start_WithCustomStartTime_SetsActivityStartTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
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
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
                startTime: customStartTime,
                endTime: customEndTime);
        });

        // Assert - Start time should be set to custom time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_SetsConversationId_WhenProvided()
    {
        // Arrange
        var conversationId = "conv-output-123";
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
                parentId: null,
                conversationId: conversationId);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
    }

    [TestMethod]
    public void Start_SetsSourceMetadata_Tags()
    {
        // Arrange
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();
        var metadata = new SourceMetadata(id: "src-id", name: "ChannelOutput", role: Role.Human, description: "https://channel/output");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
                parentId: null,
                conversationId: null,
                sourceMetadata: metadata);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiChannelNameKey, metadata.Name!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiChannelLinkKey, metadata.Description!);
    }

    [TestMethod]
    public void Start_SetsCallerDetails_WhenProvided()
    {
        // Arrange
        var callerDetails = new CallerDetails(
            callerId: "caller-output-123",
            callerName: "Output Caller",
            callerUpn: "caller-output@example.com",
            callerClientIP: System.Net.IPAddress.Parse("10.0.0.2"),
            tenantId: "tenant-output-456");
        var response = new Response(new[] { "Test message" });
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
                callerDetails: callerDetails);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiCallerIdKey, callerDetails.CallerId);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiCallerNameKey, callerDetails.CallerName);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiCallerUpnKey, callerDetails.CallerUpn);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiCallerClientIpKey, callerDetails.CallerClientIP!.ToString());
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiCallerTenantIdKey, callerDetails.TenantId!);
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
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                agentDetails,
                tenantDetails,
                response,
                startTime: customStartTime,
                endTime: initialEndTime);
            scope.SetEndTime(laterEndTime);
        });

        // Assert - The start time should be set
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }
}
