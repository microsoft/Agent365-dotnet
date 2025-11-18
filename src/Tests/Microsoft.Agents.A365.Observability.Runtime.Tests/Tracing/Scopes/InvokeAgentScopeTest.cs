namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class InvokeAgentScopeTest : ActivityTest
{
    [TestMethod]
    public void RecordResponse_ActivityTagSet()
    {
        const string expected = "response";

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            scope.RecordResponse(expected);
        });

        activity.ShouldHaveTag("gen_ai.output.messages", expected);
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
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
            scope.RecordInputMessages(messages);
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
            scope.RecordOutputMessages(messages);
        });
        activity.ShouldHaveTag("gen_ai.output.messages", string.Join(",", messages));
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void RequestContent_PopulatesInputMessagesAttribute()
    {
        // Arrange
        const string requestContent = "This is the input message content";
        var request = new Request(requestContent);

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails(), request);
        });

        // Assert
        activity.ShouldHaveTag(GenAiInputMessagesKey, requestContent);
    }
}