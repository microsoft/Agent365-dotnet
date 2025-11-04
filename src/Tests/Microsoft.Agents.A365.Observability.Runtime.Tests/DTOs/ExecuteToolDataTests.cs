// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class ExecuteToolDataTests
    {
        [TestMethod]
        public void Name_ReturnsExecuteTool()
        {
            var data = new ExecuteToolData();
            data.Name.Should().Be("ExecuteTool");
        }
    }
}
