// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class ExecuteInferenceDataTests
    {
        [TestMethod]
        public void Name_ReturnsExecuteInference()
        {
            var data = new ExecuteInferenceData();
            data.Name.Should().Be("Inference");
        }
    }
}
