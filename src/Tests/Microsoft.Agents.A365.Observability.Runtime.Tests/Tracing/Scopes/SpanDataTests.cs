namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class SpanDataTests : ActivityTest
{
    [TestMethod]
    public void SpanData_StoresProperties_IndependentOfActivity()
    {
        const string agentId = "test-agent-id";
        const string agentName = "Test Agent";
        const string toolName = "TestTool";
        const string toolArgs = "Input: 42";

        SpanData? capturedSpanData = null;
        ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails(toolName, toolArgs),
                new AgentDetails(agentId, agentName: agentName),
                Util.GetTenantDetails());

            // Capture SpanData while scope is still active
            capturedSpanData = scope?.SpanData;

            // Verify SpanData is available and contains expected values
            capturedSpanData.Should().NotBeNull();
            capturedSpanData!.Kind.Should().Be(ActivityKind.Internal);
            capturedSpanData.OperationName.Should().Be(ExecuteToolScope.OperationName);
            capturedSpanData.ActivityName.Should().Be($"{ExecuteToolScope.OperationName} {toolName}");

            // Verify SpanData contains the expected tags
            capturedSpanData.GetTag(GenAiOperationNameKey).Should().Be(ExecuteToolScope.OperationName);
            capturedSpanData.GetTag(GenAiAgentIdKey).Should().Be(agentId);
            capturedSpanData.GetTag(GenAiAgentNameKey).Should().Be(agentName);
            capturedSpanData.GetTag(GenAiToolNameKey).Should().Be(toolName);
            capturedSpanData.GetTag(GenAiToolArgumentsKey).Should().Be(toolArgs);
        });
    }

    [TestMethod]
    public void SpanData_RecordResponse_UpdatesTag()
    {
        const string response = "Test response";

        ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "Input"),
                Util.GetAgentDetails(),
                Util.GetTenantDetails());
            scope?.RecordResponse(response);

            var spanData = scope?.SpanData;
            spanData.Should().NotBeNull();
            spanData!.GetTag(GenAiEventContent).Should().Be(response);
        });
    }

    [TestMethod]
    public void SpanData_CapturesParentChildRelationship()
    {
        using var tracerProvider = ConstructTracerProvider();

        SpanData? parentSpanData = null;
        SpanData? childSpanData = null;

        ListenForActivity(() =>
        {
            using var parentScope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            parentSpanData = parentScope?.SpanData;

            using var childScope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "Input"),
                Util.GetAgentDetails(),
                Util.GetTenantDetails());
            childSpanData = childScope?.SpanData;
        });

        // Verify parent span has identifiers
        parentSpanData.Should().NotBeNull();
        parentSpanData!.TraceId.Should().NotBeNullOrEmpty("parent should have a trace ID");
        parentSpanData.SpanId.Should().NotBeNullOrEmpty("parent should have a span ID");

        // Verify child span has identifiers
        childSpanData.Should().NotBeNull();
        childSpanData!.TraceId.Should().NotBeNullOrEmpty("child should have a trace ID");
        childSpanData.SpanId.Should().NotBeNullOrEmpty("child should have a span ID");
        childSpanData.ParentSpanId.Should().NotBeNullOrEmpty("child should have a parent span ID");

        // Verify parent-child relationship
        childSpanData.TraceId.Should().Be(parentSpanData.TraceId, "child and parent should share the same trace ID");
        childSpanData.ParentSpanId.Should().Be(parentSpanData.SpanId, "child's parent span ID should match parent's span ID");
        childSpanData.SpanId.Should().NotBe(parentSpanData.SpanId, "child should have a different span ID from parent");
    }
}
