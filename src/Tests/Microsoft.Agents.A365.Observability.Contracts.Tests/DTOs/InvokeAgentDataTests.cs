// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.DTOs;

namespace Microsoft.Agents.A365.Observability.Contracts.Tests.DTOs
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
    }
}
