// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Contracts.Tests;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using static Microsoft.Agents.A365.Observability.Contracts.OpenTelemetryConstants;

[TestClass]
public sealed class ScopeTests : ActivityTest
{
    private class TestScope : OpenTelemetryScope
    {
        public TestScope(ActivityKind kind, AgentDetails agentDetails, TenantDetails tenantDetails, string operationName, string activityName)
            : base(kind, agentDetails, tenantDetails, operationName, activityName) { }
    }

    [TestMethod]
    public void NestedScope_PropagatesAgentId()
    {
        // Arrange
        using var tracerProvider = ConstructTracerProvider();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var invokeAgentScope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            using var toolScope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails(), Util.GetTenantDetails());
        });

        // Assert
        activity.Should().NotBeNull();
        activity.Kind.Should().Be(ActivityKind.Internal);
        activity.TagObjects.Should().ContainKey(GenAiOperationNameKey)
            .WhoseValue.Should().Be(ExecuteToolScope.OperationName);
        activity.TagObjects.Should().ContainKey(GenAiAgentIdKey)
            .WhoseValue.Should().BeOfType<string>()
            .Which.Should().Be(AgentId);
    }

    [TestMethod]
    public void Id_ReturnsActivityId()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        
        using var scope = new TestScope(ActivityKind.Internal, Util.GetAgentDetails(), Util.GetTenantDetails(), "TestOperation", "TestActivity");
        
        // Act
        var expectedId = scope.Id;

        // Assert
        expectedId.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void SetParentId_SetsActivityParentId()
    {
        // Arrange
        var manualParentActivity = CreateActivity();
        var parentId = manualParentActivity.Id;
        var parentSpanId = manualParentActivity.SpanId.ToString() ?? string.Empty;

        // Act
        var activity = ListenForActivity(() =>
        {
            using var toolScope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails(), Util.GetTenantDetails(), parentId);
        });
        
        // Assert
        activity.Should().NotBeNull();
        activity!.ParentSpanId.ToString().Should().Be(parentSpanId);
    }
}
