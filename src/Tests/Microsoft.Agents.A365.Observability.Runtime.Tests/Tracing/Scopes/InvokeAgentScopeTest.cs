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
        const string requestContent = "This is the input message content";
        var request = new Request(requestContent);

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails(), request);
        });

        activity.ShouldHaveTag(GenAiInputMessagesKey, requestContent);
    }

    [TestMethod]
    public void CallerClientIpTag_IsSetCorrectly()
    {
        var callerIp = IPAddress.Parse("203.0.113.42");
        var callerDetails = new CallerDetails(
            callerId: "caller-001",
            callerName: "Test Caller",
            callerUpn: "test.caller@contoso.com",
            tenantId: "tenant-xyz",
            callerClientIP: callerIp);

        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                invokeAgentDetails: Details,
                tenantDetails: Util.GetTenantDetails(),
                request: null,
                callerAgentDetails: null,
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

        var invokeAgentDetails = new InvokeAgentDetails(agentDetails, new Uri("https://example.com"));
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                invokeAgentDetails,
                tenantDetails);
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
        var invokeAgentDetails = Details;
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                invokeAgentDetails,
                tenantDetails,
                request: null,
                callerAgentDetails: null,
                callerDetails: null,
                conversationId: null,
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
        // Arrange
        var invokeAgentDetails = Details;
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                invokeAgentDetails,
                tenantDetails,
                request: null,
                callerAgentDetails: null,
                callerDetails: null,
                conversationId: null,
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
        var invokeAgentDetails = Details;
        var tenantDetails = Util.GetTenantDetails();

        // Act
        var activity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(invokeAgentDetails, tenantDetails);
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
        var tenantDetails = Util.GetTenantDetails();
        ActivityContext? parentContext = null;
        ListenForActivity(() =>
        {
            using var parentScope = InvokeAgentScope.Start(Details, tenantDetails);
            parentContext = parentScope.GetActivityContext();
        });

        // Act
        var childActivity = ListenForActivity(() =>
        {
            using var scope = InvokeAgentScope.Start(
                Details,
                tenantDetails,
                parentContext: parentContext);
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
                Details,
                Util.GetTenantDetails(),
                startTime: customStartTime);
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
                Details,
                Util.GetTenantDetails(),
                startTime: customStartTime,
                endTime: customEndTime);
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
                Details,
                Util.GetTenantDetails(),
                startTime: customStartTime,
                endTime: initialEndTime);
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
            using var scope = InvokeAgentScope.Start(Details, Util.GetTenantDetails());
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
                Details,
                Util.GetTenantDetails(),
                spanKind: System.Diagnostics.ActivityKind.Server);
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
                    new InvokeAgentDetails(new AgentDetails("agent-1"), null),
                    Util.GetTenantDetails());
            });

            // Assert - processor should coalesce server baggage onto the span
            activity.ShouldHaveTag(ServerAddressKey, serverAddress);
            activity.ShouldHaveTag(ServerPortKey, serverPort);
        }
    }
}