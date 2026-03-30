// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class ScopeTests : ActivityTest
{
    private class TestScope : OpenTelemetryScope
    {
        public TestScope(string operationName, string activityName, AgentDetails agentDetails, SpanDetails? spanDetails = null)
            : base(operationName, activityName, agentDetails, spanDetails) { }
    }

    [TestMethod]
    public void NestedScope_PropagatesAgentId()
    {
        // Arrange
        using var tracerProvider = ConstructTracerProvider();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var invokeAgentScope = InvokeAgentScope.Start(new Request(), ScopeDetails, Util.GetAgentDetails());
            using var toolScope = ExecuteToolScope.Start(new Request(), new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails());
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
        
        using var scope = new TestScope("TestOperation", "TestActivity", Util.GetAgentDetails(), new SpanDetails(ActivityKind.Internal));
        
        // Act
        var expectedId = scope.Id;

        // Assert
        expectedId.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void SetParentContext_SetsActivityParentId()
    {
        // Arrange
        var manualParentActivity = CreateActivity();
        var parentContext = manualParentActivity.Context;
        var parentSpanId = manualParentActivity.SpanId.ToString() ?? string.Empty;

        // Act
        var activity = ListenForActivity(() =>
        {
            using var toolScope = ExecuteToolScope.Start(new Request(), new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails(), spanDetails: new SpanDetails(parentContext: parentContext));
        });
        
        // Assert
        activity.Should().NotBeNull();
        activity!.ParentSpanId.ToString().Should().Be(parentSpanId);
    }
}
