// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.DTOs;

namespace Microsoft.Agents.A365.Observability.Contracts.Tests.DTOs
{
    [TestClass]
    public class ExecuteInferenceDataTests
    {
        [TestMethod]
        public void Name_ReturnsExecuteInference()
        {
            var data = new ExecuteInferenceData();
            data.Name.Should().Be(OpenTelemetryConstants.OperationNames.ExecuteInference.ToString());
        }
    }
}
