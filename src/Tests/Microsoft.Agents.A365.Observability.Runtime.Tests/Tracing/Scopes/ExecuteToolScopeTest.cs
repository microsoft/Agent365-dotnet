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

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiEventContent, expected);
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
    public void AgentTypeTag_IsSetCorrectly()
    {
        // Arrange
        var agentType = AgentType.MicrosoftCopilot;
        var agentDetails = new AgentDetails(
            agentId: "agent-xyz",
            agentName: "ToolAgent",
            agentType: agentType);

        var toolCallDetails = new ToolCallDetails("TestTool", "args");
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(toolCallDetails, agentDetails, tenantDetails);
        });

        // Assert
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiAgentTypeKey, agentType.ToString());
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
                parentId: null,
                conversationId: conversationId,
                sourceMetadata: null);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiConversationIdKey, conversationId);
    }

    [TestMethod]
    public void Start_SetsSourceMetadata_Tags()
    {
        var metadata = new SourceMetadata(id: "tool-src", name: "ChannelY", role: Role.Agent, description: "https://channel/link/y");

        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"),
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                parentId: null,
                conversationId: null,
                sourceMetadata: metadata);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiChannelNameKey, metadata.Name!);
        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiChannelLinkKey, metadata.Description!);
    }
}