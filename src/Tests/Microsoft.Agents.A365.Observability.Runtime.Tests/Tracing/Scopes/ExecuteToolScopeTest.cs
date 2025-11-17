// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts;
using Microsoft.Agents.A365.Observability.Contracts.Details;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

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
            scope.RecordResponse(expected);
        });

        activity.ShouldHaveTag(OpenTelemetryConstants.GenAiEventContent, expected);
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(new ToolCallDetails("TestTool", "x"), Util.GetAgentDetails(), Util.GetTenantDetails());
            scope?.RecordError(new Exception(expected));
        });
        
        activity.ShouldBeError(expected);
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var activity = ListenForActivity(() =>
        {
            using var scope = ExecuteToolScope.Start(
                new ToolCallDetails("TestTool", "args"), 
                Util.GetAgentDetails(), 
                Util.GetTenantDetails());
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

}