namespace Microsoft.Agents.A365.Observability.Tests.Middleware;

using System.Net;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.Common;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
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
        var corr = "corr-1";
        var session = "session-1";
        var sessionDescription = "Test Session";
        var callerClientIp = IPAddress.Parse("203.0.113.42");

        // Act
        using (new BaggageBuilder()
            .TenantId(tenant)
            .AgentId(agent)
            .CorrelationId(corr)
            .SessionId(session)
            .SessionDescription(sessionDescription)
            .AgentType(AgentType.EntraEmbodied)
            .CallerClientIp(callerClientIp)
            .Build())
        {
            // Assert inside scope
            Baggage.Current.GetBaggage(TenantIdKey).Should().Be(tenant);
            Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().Be(agent);
            Baggage.Current.GetBaggage(CorrelationIdKey).Should().Be(corr);
            Baggage.Current.GetBaggage(SessionIdKey).Should().Be(session);
            Baggage.Current.GetBaggage(SessionDescriptionKey).Should().Be(sessionDescription);
            Baggage.Current.GetBaggage(GenAiAgentTypeKey).Should().Be(AgentType.EntraEmbodied.ToString());
            Baggage.Current.GetBaggage(GenAiCallerClientIpKey).Should().Be(callerClientIp.ToString());
        }

        // Assert after dispose (restored -> no values)
        Baggage.Current.GetBaggage(TenantIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(CorrelationIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(SessionIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(SessionDescriptionKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiAgentTypeKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiCallerClientIpKey).Should().BeNull();
    }
    
}