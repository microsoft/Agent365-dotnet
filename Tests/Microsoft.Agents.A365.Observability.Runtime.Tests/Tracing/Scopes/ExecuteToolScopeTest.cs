namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

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
            scope?.RecordResponse(expected);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiEventContent, expected);
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