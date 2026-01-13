// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class OutputScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_SetsOperationName()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), Util.GetTenantDetails());
        });

        activity.ShouldHaveTag(GenAiOperationNameKey, OutputScope.OperationName);
    }

    [TestMethod]
    public void Start_SetsActivityKindToClient()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), Util.GetTenantDetails());
        });

        activity.Kind.Should().Be(ActivityKind.Client);
    }

    [TestMethod]
    public void Start_SetsAgentDetails()
    {
        var agentDetails = new AgentDetails(
            agentId: "agent-123",
            agentName: "TestAgent",
            agentDescription: "Test agent description",
            agentAUID: "auid-456",
            agentUPN: "agent@contoso.com",
            agentBlueprintId: "blueprint-789",
            agentType: AgentType.Foundry,
            agentPlatformId: "platform-001");

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, Util.GetTenantDetails());
        });

        activity.ShouldHaveTag(GenAiAgentIdKey, "agent-123");
        activity.ShouldHaveTag(GenAiAgentNameKey, "TestAgent");
        activity.ShouldHaveTag(GenAiAgentDescriptionKey, "Test agent description");
        activity.ShouldHaveTag(GenAiAgentAUIDKey, "auid-456");
        activity.ShouldHaveTag(GenAiAgentUPNKey, "agent@contoso.com");
        activity.ShouldHaveTag(GenAiAgentBlueprintIdKey, "blueprint-789");
        activity.ShouldHaveTag(GenAiAgentTypeKey, AgentType.Foundry.ToString());
        activity.ShouldHaveTag(GenAiAgentPlatformIdKey, "platform-001");
    }

    [TestMethod]
    public void Start_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var tenantDetails = new TenantDetails(tenantId);

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), tenantDetails);
        });

        // Verify tenant.id tag is set by checking TagObjects (which includes non-string tags)
        activity.TagObjects.Should().ContainKey(TenantIdKey, "Activity should have tag 'tenant.id'")
            .WhoseValue.Should().Be(tenantId);
    }

    [TestMethod]
    public void Start_SetsSessionId()
    {
        const string sessionId = "session-123";

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                sessionId: sessionId);
        });

        activity.ShouldHaveTag(SessionIdKey, sessionId);
    }

    [TestMethod]
    public void Start_SetsSessionDescription()
    {
        const string sessionDescription = "Test session description";

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                sessionDescription: sessionDescription);
        });

        activity.ShouldHaveTag(SessionDescriptionKey, sessionDescription);
    }

    [TestMethod]
    public void Start_SetsConversationId()
    {
        const string conversationId = "conv-456";

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                conversationId: conversationId);
        });

        activity.ShouldHaveTag(GenAiConversationIdKey, conversationId);
    }

    [TestMethod]
    public void Start_SetsExecutionType()
    {
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                executionType: ExecutionType.HumanToAgent);
        });

        activity.ShouldHaveTag(GenAiExecutionTypeKey, ExecutionType.HumanToAgent.ToString());
    }

    [TestMethod]
    public void Start_SetsCallerDetails()
    {
        var callerIp = IPAddress.Parse("192.168.1.100");
        var callerDetails = new CallerDetails(
            callerId: "caller-001",
            callerName: "Test Caller",
            callerUpn: "test.caller@contoso.com",
            callerClientIP: callerIp,
            tenantId: "tenant-xyz");

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                callerDetails: callerDetails);
        });

        activity.ShouldHaveTag(GenAiCallerIdKey, "caller-001");
        activity.ShouldHaveTag(GenAiCallerNameKey, "Test Caller");
        activity.ShouldHaveTag(GenAiCallerUpnKey, "test.caller@contoso.com");
        activity.ShouldHaveTag(GenAiCallerClientIpKey, callerIp.ToString());
        activity.ShouldHaveTag(GenAiCallerTenantIdKey, "tenant-xyz");
    }

    [TestMethod]
    public void Start_SetsChannelMetadataFromSourceMetadata()
    {
        var sourceMetadata = new SourceMetadata(
            name: "TestChannel",
            role: Role.Human,
            description: "Test channel description");

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                sourceMetadata: sourceMetadata);
        });

        activity.ShouldHaveTag(GenAiChannelNameKey, "TestChannel");
        activity.ShouldHaveTag(GenAiChannelLinkKey, "Test channel description");
    }

    [TestMethod]
    public void RecordOutputMessages_SetsTag()
    {
        var messages = new[] { "Hello!", "Here is your response." };

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), Util.GetTenantDetails());
            scope.RecordOutputMessages(messages);
        });

        activity.ShouldHaveTag(GenAiOutputMessagesKey, string.Join(",", messages));
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), Util.GetTenantDetails());
            scope.RecordError(new Exception(expected));
        });

        activity.ShouldBeError(expected);
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(Util.GetAgentDetails(), Util.GetTenantDetails());
            scope.SetStartTime(customStartTime);
        });

        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void SetParentId_SetsActivityParentId()
    {
        var manualParentActivity = CreateActivity();
        var parentId = manualParentActivity.Id;
        var parentSpanId = manualParentActivity.SpanId.ToString() ?? string.Empty;

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(
                Util.GetAgentDetails(),
                Util.GetTenantDetails(),
                parentId: parentId);
        });

        activity.Should().NotBeNull();
        activity!.ParentSpanId.ToString().Should().Be(parentSpanId);
    }

    [TestMethod]
    public void ActivityName_UsesAgentName_WhenProvided()
    {
        var agentDetails = new AgentDetails(
            agentId: "agent-123",
            agentName: "MyTestAgent");

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, Util.GetTenantDetails());
        });

        activity.DisplayName.Should().Be("output_messages MyTestAgent");
    }

    [TestMethod]
    public void ActivityName_UsesOperationName_WhenAgentNameIsNull()
    {
        var agentDetails = new AgentDetails(agentId: "agent-123");

        var activity = ListenForActivity(() =>
        {
            using var scope = OutputScope.Start(agentDetails, Util.GetTenantDetails());
        });

        activity.DisplayName.Should().Be(OutputScope.OperationName);
    }
}
