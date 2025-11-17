// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.DTOs;

namespace Microsoft.Agents.A365.Observability.Contracts.Tests.DTOs
{
    [TestClass]
    public class ExecuteToolDataTests
    {
        [TestMethod]
        public void Name_ReturnsExecuteTool()
        {
            var data = new ExecuteToolData();
            data.Name.Should().Be(OpenTelemetryConstants.OperationNames.ExecuteTool.ToString());
        }
    }
}
