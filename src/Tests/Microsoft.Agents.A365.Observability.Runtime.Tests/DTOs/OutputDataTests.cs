// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class OutputDataTests
    {
        [TestMethod]
        public void Name_ReturnsOutputMessages()
        {
            var data = new OutputData();
            data.Name.Should().Be(OpenTelemetryConstants.OperationNames.OutputMessages.ToString());
        }
    }
}
