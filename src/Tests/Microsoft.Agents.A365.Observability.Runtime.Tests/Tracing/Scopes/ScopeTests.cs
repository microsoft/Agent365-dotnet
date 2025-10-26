namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class ScopeTests : ActivityTest
{
    [TestMethod]
    public void NestedScope_PropagatesAgentId()
    {
        using var tracerProvider = ConstructTracerProvider();

        var activity = ListenForActivity(() =>
        {
            using var invokeAgentScope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            using var toolScope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "Input: 42"), Util.GetAgentDetails(), Util.GetTenantDetails());
        });
        
        activity.Should().NotBeNull();
        activity.Kind.Should().Be(ActivityKind.Internal);
        activity.TagObjects.Should().ContainKey(GenAiOperationNameKey)
            .WhoseValue.Should().Be(ExecuteToolScope.OperationName);
        activity.TagObjects.Should().ContainKey(GenAiAgentIdKey)
            .WhoseValue.Should().BeOfType<string>()
            .Which.Should().Be(AgentId);
    }
}