// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.Common;

public partial class ExportFormatterTests
{
    [TestMethod]
    public void FormatLogData_NoError_EmitsUnsetStatus()
    {
        // Arrange
        var data = new InvokeAgentData(
            new Dictionary<string, object?> { { "key", "val" } },
            spanId: "span-1");
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("Status");
        status.GetProperty("code").GetInt32().Should().Be(0);
        status.GetProperty("message").GetString().Should().Be("");
    }

    [TestMethod]
    public void FormatLogData_WithErrorStatus_EmitsErrorCodeAndMessage()
    {
        // Arrange
        var data = new InvokeAgentData(
            new Dictionary<string, object?> { { "key", "val" } },
            spanId: "span-2")
        {
            StatusCode = SpanStatusCode.Error,
            StatusMessage = "something failed"
        };
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(data.ToDictionary());

        // Assert
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("Status");
        status.GetProperty("code").GetInt32().Should().Be(2);
        status.GetProperty("message").GetString().Should().Be("something failed");
    }

    [TestMethod]
    public void FormatLogData_MissingStatusKey_DefaultsToUnset()
    {
        // Arrange — a dictionary without a "Status" entry
        var bareData = new Dictionary<string, object?>
        {
            { "Name", "InvokeAgent" },
            { "Attributes", new Dictionary<string, object?>() },
            { "SpanId", "span-3" },
            { "ParentSpanId", null },
        };
        var formatter = CreateFormatter();

        // Act
        var json = formatter.FormatLogData(bareData);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("Status");
        status.GetProperty("code").GetInt32().Should().Be(0);
        status.GetProperty("message").GetString().Should().Be("");
    }
}
