using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class InvokeAgentDataTests
    {
        [TestMethod]
        public void Name_ReturnsInvokeAgent()
        {
            var data = new InvokeAgentData();
            data.Name.Should().Be("InvokeAgent");
        }
    }
}
