// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Agents.A365.Observability.Tests.Middleware;

using System.Net;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;

using static Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes.OpenTelemetryConstants;

[TestClass]
public sealed class BaggageBuilderTest
{
    [TestInitialize]
    public void EnableTelemetry()
    {
        AppContext.SetSwitch(EnableOpenTelemetrySwitch, true);
    }

    [TestMethod]
    public void Apply_SetsAndRestores_BaggageValues()
    {
        // Arrange
        Baggage.Current = default; // clear prior test pollution
        var tenant = "tenant-1";
        var agent = "agent-1";
        var session = "session-1";
        var sessionDescription = "Test Session";
        var callerClientIp = IPAddress.Parse("203.0.113.42");
        var platformId = "platform-123";

        // Act
        using (new BaggageBuilder()
            .TenantId(tenant)
            .AgentId(agent)
            .SessionId(session)
            .SessionDescription(sessionDescription)
            .CallerClientIp(callerClientIp)
            .AgentPlatformId(platformId)
            .Build())
        {
            // Assert inside scope
            Baggage.Current.GetBaggage(TenantIdKey).Should().Be(tenant);
            Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().Be(agent);
            Baggage.Current.GetBaggage(SessionIdKey).Should().Be(session);
            Baggage.Current.GetBaggage(SessionDescriptionKey).Should().Be(sessionDescription);
            Baggage.Current.GetBaggage(CallerClientIpKey).Should().Be(callerClientIp.ToString());
            Baggage.Current.GetBaggage(GenAiAgentPlatformIdKey).Should().Be(platformId);
        }

        // Assert after dispose (restored -> no values)
        Baggage.Current.GetBaggage(TenantIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(SessionIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(SessionDescriptionKey).Should().BeNull();
        Baggage.Current.GetBaggage(CallerClientIpKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiAgentPlatformIdKey).Should().BeNull();
    }
}