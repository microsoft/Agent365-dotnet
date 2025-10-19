namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class InvokeAgentScopeTest : ActivityTest
{
    [TestMethod]
    public void Start_Request_Set()
    {
        const string expected = "response";

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails(), new Request(expected));
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiRequestContentKey, expected);
    }

    [TestMethod]
    public void RecordResponse_ActivityTagSet()
    {
        const string expected = "response";

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
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

    [TestMethod]
    public void RecordInputMessages_ActivityTagSet()
    {
        var messages = new[] { "Hello", "How are you?" };
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            scope?.RecordInputMessages(messages);
        });
        activity.ShouldHaveTag("gen_ai.input.messages", string.Join(",", messages));
    }

    [TestMethod]
    public void RecordOutputMessages_ActivityTagSet()
    {
        var messages = new[] { "Hi there!", "I'm fine." };
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            scope?.RecordOutputMessages(messages);
        });
        activity.ShouldHaveTag("gen_ai.output.messages", string.Join(",", messages));
    }
}