// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.Tests;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

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
}