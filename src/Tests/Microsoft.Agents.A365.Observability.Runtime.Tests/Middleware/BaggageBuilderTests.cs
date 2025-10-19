namespace Microsoft.Agents.A365.Observability.Tests.Middleware;

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
        var corr = "corr-1";

        // Act
        using (new BaggageBuilder()
            .TenantId(tenant)
            .AgentId(agent)
            .CorrelationId(corr)
            .Build())
        {
            // Assert inside scope
            Baggage.Current.GetBaggage(TenantIdKey).Should().Be(tenant);
            Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().Be(agent);
            Baggage.Current.GetBaggage(CorrelationIdKey).Should().Be(corr);
        }

        // Assert after dispose (restored -> no values)
        Baggage.Current.GetBaggage(TenantIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(GenAiAgentIdKey).Should().BeNull();
        Baggage.Current.GetBaggage(CorrelationIdKey).Should().BeNull();
    }
    
}