// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Tracing.Scopes;

using System;
using System.Diagnostics;
using System.Net;
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
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            scope.RecordResponse(expected);
        });

        var tagValue = activity.Tags.First(t => t.Key == "gen_ai.output.messages").Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"assistant\"");
        tagValue.Should().Contain(expected);
    }

    [TestMethod]
    public void RecordError_SetsExpectedFields()
    {
        const string expected = "Test error";
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
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
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            scope.RecordInputMessages(messages);
        });

        var tagValue = activity.Tags.First(t => t.Key == "gen_ai.input.messages").Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"user\"");
        tagValue.Should().Contain("Hello");
        tagValue.Should().Contain("How are you?");
    }

    [TestMethod]
    public void RecordOutputMessages_ActivityTagSet()
    {
        var messages = new[] { "Hi there!", "I'm fine." };
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            scope.RecordOutputMessages(messages);
        });

        var tagValue = activity.Tags.First(t => t.Key == "gen_ai.output.messages").Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"assistant\"");
        tagValue.Should().Contain("Hi there!");
        tagValue.Should().Contain("I\\u0027m fine.");
    }

    [TestMethod]
    public void SetStartTime_SetsActivityStartTime()
    {
        var customStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            scope.SetStartTime(customStartTime);
        });

        // Activity start time should be close to the custom start time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void RequestContent_PopulatesInputMessagesAttribute()
    {
        const string requestContent = "This is the input message content";
        var request = new Request(content: requestContent);

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(request, ScopeDetails, TestAgentDetails);
        });

        var tagValue = activity.Tags.First(t => t.Key == GenAiInputMessagesKey).Value;
        tagValue.Should().Contain("\"version\":\"0.1.0\"");
        tagValue.Should().Contain("\"role\":\"user\"");
        tagValue.Should().Contain(requestContent);
    }

    [TestMethod]
    public void CallerClientIpTag_IsSetCorrectly()
    {
        var callerIp = IPAddress.Parse("203.0.113.42");
        var userDetails = new UserDetails(
            userId: "caller-001",
            userName: "Test Caller",
            userEmail: "test.caller@contoso.com",
            userClientIP: callerIp);
        var callerDetails = new CallerDetails(userDetails: userDetails);

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                request: Util.GetDefaultRequest(),
                scopeDetails: ScopeDetails,
                agentDetails: TestAgentDetails,
                callerDetails: callerDetails);
        });

        // Assert
        activity.ShouldHaveTag(CallerClientIpKey, callerIp.ToString());
    }

    [TestMethod]
    public void AgentPlatformIdTag_IsSetCorrectly()
    {
        // Arrange
        var platformId = "platform-001";
        var agentDetails = new AgentDetails(
            agentId: "agent-789",
            agentName: "PlatformAgent",
            agentPlatformId: platformId);

        var scopeDetails = new InvokeAgentScopeDetails(endpoint: new Uri("https://example.com"));

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                scopeDetails,
                agentDetails);
        });

        // Assert
        activity.ShouldHaveTag(AgentPlatformIdKey, platformId);
    }

    [TestMethod]
    public void ThreatDiagnosticsSummary_IsSetCorrectly_WhenProvided()
    {
        // Arrange
        var threatSummary = new ThreatDiagnosticsSummary(
            blockAction: true,
            reasonCode: 112,
            reason: "The action was blocked because there is a noncompliant email address in the BCC field.",
            diagnostics: "{\"flaggedField\":\"bcc\",\"flaggedValue\":\"hacker@evil.com\"}");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                request: Util.GetDefaultRequest(),
                scopeDetails: ScopeDetails,
                agentDetails: TestAgentDetails,
                threatDiagnosticsSummary: threatSummary);
        });

        // Assert - use Contains checks to handle JSON Unicode encoding variations
        var tagValue = activity.Tags.First(t => t.Key == ThreatDiagnosticsSummaryKey).Value;
        tagValue.Should().Contain("\"blockAction\":true");
        tagValue.Should().Contain("\"reasonCode\":112");
        tagValue.Should().Contain("\"reason\":\"The action was blocked because there is a noncompliant email address in the BCC field.\"");
        tagValue.Should().Contain("flaggedField");
        tagValue.Should().Contain("bcc");
        tagValue.Should().Contain("hacker@evil.com");
    }

    [TestMethod]
    public void ThreatDiagnosticsSummary_IsNotSet_WhenNull()
    {
        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                request: Util.GetDefaultRequest(),
                scopeDetails: ScopeDetails,
                agentDetails: TestAgentDetails,
                threatDiagnosticsSummary: null);
        });

        // Assert
        activity.Tags.Should().NotContainKey(ThreatDiagnosticsSummaryKey);
    }

    [TestMethod]
    public void RecordThreatDiagnosticsSummary_SetsTagCorrectly()
    {
        // Arrange
        var threatSummary = new ThreatDiagnosticsSummary(
            blockAction: true,
            reasonCode: 200,
            reason: "Blocked due to policy violation.",
            diagnostics: "{\"policy\":\"data-loss-prevention\"}");

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            scope.RecordThreatDiagnosticsSummary(threatSummary);
        });

        // Assert
        var tagValue = activity.Tags.First(t => t.Key == ThreatDiagnosticsSummaryKey).Value;
        tagValue.Should().Contain("\"blockAction\":true");
        tagValue.Should().Contain("\"reasonCode\":200");
        tagValue.Should().Contain("\"reason\":\"Blocked due to policy violation.\"");
        tagValue.Should().Contain("data-loss-prevention");
    }

    [TestMethod]
    public void Start_WithParentContext_SetsParentOnActivity()
    {
        // Arrange
        ActivityContext? parentContext = null;
        ListenForActivity(() =>
        {
            using var parentScope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
            parentContext = parentScope.GetActivityContext();
        });

        // Act
        var childActivity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(parentContext: parentContext));
        });

        // Assert
        childActivity.ParentSpanId.ToString().Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void Start_WithCustomStartTime_SetsActivityStartTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(startTime: customStartTime));
        });

        // Assert
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void Start_WithCustomStartAndEndTime_SetsActivityTimes()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);
        var customEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 25, TimeSpan.Zero); // 5 seconds later

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: customEndTime));
        });

        // Assert - Start time should be set to custom time
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void SetEndTime_OverridesEndTime()
    {
        // Arrange
        var customStartTime = new DateTimeOffset(2023, 11, 14, 22, 13, 40, TimeSpan.Zero);
        var initialEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 45, TimeSpan.Zero);
        var laterEndTime = new DateTimeOffset(2023, 11, 14, 22, 13, 48, TimeSpan.Zero);

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(startTime: customStartTime, endTime: initialEndTime));
            scope.SetEndTime(laterEndTime);
        });

        // Assert - The start time should be set
        var startTime = new DateTimeOffset(activity.StartTimeUtc);
        startTime.Should().BeCloseTo(customStartTime, TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public void SpanKind_DefaultsToClient()
    {
        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Util.GetDefaultRequest(), ScopeDetails, TestAgentDetails);
        });

        // Assert
        activity.Kind.Should().Be(System.Diagnostics.ActivityKind.Client);
    }

    [TestMethod]
    public void SpanKind_OverrideToServer()
    {
        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Util.GetDefaultRequest(),
                ScopeDetails,
                TestAgentDetails,
                spanDetails: new SpanDetails(spanKind: System.Diagnostics.ActivityKind.Server));
        });

        // Assert
        activity.Kind.Should().Be(System.Diagnostics.ActivityKind.Server);
    }

    [TestMethod]
    public void ActivityProcessor_PropagatesServerBaggage_ForInvokeAgentSpan()
    {
        // Arrange
        using var tracerProvider = ConstructTracerProvider();
        var serverAddress = "myagent.azurewebsites.net";
        var serverPort = "8443";

        // Act - set server address/port in baggage, then start an invoke_agent span
        using (new Runtime.Common.BaggageBuilder()
            .InvokeAgentServer(serverAddress, 8443)
            .Build())
        {
            var activity = ListenForActivity(() =>
            {
                using var scope = InvokeAgentScope.Start(
                    Util.GetDefaultRequest(),
                    new InvokeAgentScopeDetails(endpoint: null),
                    new AgentDetails("agent-1"));
            });

            // Assert - processor should coalesce server baggage onto the span
            activity.ShouldHaveTag(ServerAddressKey, serverAddress);
            activity.ShouldHaveTag(ServerPortKey, serverPort);
        }
    }
}
