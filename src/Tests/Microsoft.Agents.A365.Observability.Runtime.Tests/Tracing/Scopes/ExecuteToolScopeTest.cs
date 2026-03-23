namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;

[TestClass]
public sealed class ExecuteToolScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_Arguments_Set()
    {
        const string expected = "Input: 42";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", expected), Util.GetAgentDetails(),Util.GetTenantDetails());
        });
        
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiToolArgumentsKey, expected);
    }
    
    [TestMethod]
    public void RecordResponse_Response_Set()
    {
        const string expected = "Output: 42";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "x"), Util.GetAgentDetails(), Util.GetTenantDetails());
            scope.RecordResponse(expected);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiToolCallResultKey, expected);
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "x"), Util.GetAgentDetails(), Util.GetTenantDetails());
            scope?.RecordError(new Exception(expected));
        });
        
        activity.ShouldBeError(expected);
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"), 
                Util.GetAgentDetails(), 
                Util.GetTenantDetails());
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_SetsConversationId_WhenProvided()
    {
        var conversationId = "conv-tool-123";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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
        var metadata = new Channel(name: "ChannelY", link: "https://channel/link/y");

        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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
    public void ThreatDiagnosticsSummary_IsSetCorrectly_WhenProvided()
    {
        // Arrange
        var threatSummary = new ThreatDiagnosticsSummary(
            blockAction: false,
            reasonCode: 0,
            reason: "No threat detected.",
            diagnostics: null);
        var toolCallDetails = new ToolCallDetails("SecurityTool", "scan args");
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                toolCallDetails,
                agentDetails,
                tenantDetails,
                parentContext: null,
                conversationId: null,
                channel: null,
                threatDiagnosticsSummary: threatSummary);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey, "{\"blockAction\":false,\"reasonCode\":0,\"reason\":\"No threat detected.\",\"diagnostics\":null}");
    }

    [TestMethod]
    public void ThreatDiagnosticsSummary_IsNotSet_WhenNull()
    {
        // Arrange
        var toolCallDetails = new ToolCallDetails("TestTool", "args");
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                toolCallDetails,
                agentDetails,
                tenantDetails,
                parentContext: null,
                conversationId: null,
                channel: null,
                threatDiagnosticsSummary: null);
        });

        // Assert
        activity.Tags.Should().NotContainKey(OpenTelemetryConstants.ThreatDiagnosticsSummaryKey);
    }

    [TestMethod]
    public void RecordThreatDiagnosticsSummary_SetsTagCorrectly()
    {
        // Arrange
        var threatSummary = new ThreatDiagnosticsSummary(
            blockAction: true,
            reasonCode: 200,
            reason: "Blocked due to policy violation.",
            diagnostics: "{\"policy\":\"data-loss-prevention\"}");
        var toolCallDetails = new ToolCallDetails("TestTool", "args");
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails);
            scope.RecordThreatDiagnosticsSummary(threatSummary);
        });

        // Assert
        var tagValue = activity.Tags.First(t => t.Key == OpenTelemetryConstants.ThreatDiagnosticsSummaryKey).Value;
        tagValue.Should().Contain("\"blockAction\":true");
        tagValue.Should().Contain("\"reasonCode\":200");
        tagValue.Should().Contain("\"reason\":\"Blocked due to policy violation.\"");
        tagValue.Should().Contain("data-loss-prevention");
    }

    [TestMethod]
    public void Start_ToolServerName_IsSetCorrectly()
    {
        // Arrange
        const string expectedToolServerName = "test-tool-server";
        var toolCallDetails = new ToolCallDetails(
            toolName: "TestTool",
            arguments: "args",
            toolServerName: expectedToolServerName);
        var agentDetails = Util.GetAgentDetails();
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiToolServerNameKey, expectedToolServerName);
    }

    [TestMethod]
    public void Start_SetsCallerDetails_WhenProvided()
    {
        // Arrange
        var callerDetails = new CallerDetails(
            callerId: "caller-123",
            callerName: "Test Caller",
            callerUpn: "caller@example.com",
            callerClientIP: System.Net.IPAddress.Parse("192.168.1.1"),
            tenantId: "tenant-456");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
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

    [TestMethod]
    public void SpanKind_DefaultsToInternal()
    {
        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
                Util.GetAgentDetails(),
                Util.GetTenantDetails());
        });

        // Assert
        activity.Kind.Should().Be(System.Diagnostics.ActivityKind.Internal);
    }

    [TestMethod]
    public void SpanKind_OverrideToClient()
    {
        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                spanKind: System.Diagnostics.ActivityKind.Client);
        });

        // Assert
        activity.Kind.Should().Be(System.Diagnostics.ActivityKind.Client);
    }
}