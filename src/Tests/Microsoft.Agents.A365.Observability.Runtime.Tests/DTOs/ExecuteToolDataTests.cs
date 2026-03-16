// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
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

        [TestMethod]
        public void SpanKind_DefaultsToNull()
        {
            var data = new ExecuteToolData();
            data.SpanKind.Should().BeNull();
        }

        [TestMethod]
        public void SpanKind_UsesProvidedValue()
        {
            var data = new ExecuteToolData(spanKind: SpanKindConstants.Client);
            data.SpanKind.Should().Be(SpanKindConstants.Client);
        }
    }
}
