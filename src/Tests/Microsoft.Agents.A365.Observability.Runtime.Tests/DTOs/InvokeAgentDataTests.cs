using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class InvokeAgentDataTests
    {
        [TestMethod]
        public void Name_ReturnsInvokeAgent()
        {
            var data = new InvokeAgentData();
            data.Name.Should().Be(OpenTelemetryConstants.OperationNames.InvokeAgent.ToString());
        }

        [TestMethod]
        public void SpanKind_DefaultsToNull()
        {
            var data = new InvokeAgentData();
            data.SpanKind.Should().BeNull();
        }

        [TestMethod]
        public void SpanKind_UsesProvidedValue()
        {
            var data = new InvokeAgentData(spanKind: SpanKindConstants.Server);
            data.SpanKind.Should().Be(SpanKindConstants.Server);
        }
    }
}
