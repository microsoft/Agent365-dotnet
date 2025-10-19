namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class ExecuteAgentScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_AgentId_InBaggage()
    {
        const string expected = AgentId;
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteAgentScope.Start(expected, Util.GetTenantDetails().TenantId);
        });
        
        activity.ShouldHaveTag(GenAiAgentIdKey, expected);
        activity.Baggage.Should().ContainKey(GenAiAgentIdKey)
            .WhoseValue.Should().Be(expected);
    }
    
    [TestMethod]
    public void Start_Request_Set()
    {
        const string expected = "response";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteAgentScope.Start(AgentId, Util.GetTenantDetails().TenantId, new Request(expected));
        });

        activity.ShouldHaveTag(GenAiRequestContentKey, expected);
    }

    [TestMethod]
    public void RecordResponse_()
    {
        const string expected = "response";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteAgentScope.Start(AgentId, Util.GetTenantDetails().TenantId);
            scope?.RecordResponse(expected);
        });
        
        activity.ShouldHaveTag(GenAiEventContent, expected);
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteAgentScope.Start(AgentId, Util.GetTenantDetails().TenantId);
            scope?.RecordError(new Exception(expected));
        });
        
        activity.ShouldBeError(expected);
    }

}