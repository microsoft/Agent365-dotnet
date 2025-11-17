// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Contracts.DTOs;
using System.Text.Json;

namespace Microsoft.Agents.A365.Observability.Contracts.Tests
{
    [TestClass]
    public sealed class LogSerializerTests
    {
        [TestMethod]
        public void Serialize_WithAllFields_ProducesExpectedJson()
        {
            // Arrange
            var start = DateTimeOffset.UtcNow.AddMinutes(-1).DateTime;
            var end = DateTimeOffset.UtcNow.DateTime;
            var spanId = "span-123";
            var parentSpanId = "parent-456";
            var data = new InvokeAgentData(
                new Dictionary<string, object?>
                {
                { "attr1", "value1" },
                { "attr2", 42 }
                },
                start,
                end,
                spanId,
                parentSpanId);

            // Act
            var json = LogSerializer.Serialize(data.ToDictionary());

            // Assert
            json.Should().NotBeNullOrWhiteSpace();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            root.GetProperty("Name").GetString().Should().Be("InvokeAgent");
            root.GetProperty("SpanId").GetString().Should().Be(spanId);
            root.GetProperty("ParentSpanId").GetString().Should().Be(parentSpanId);

            var attrs = root.GetProperty("Attributes");
            attrs.GetProperty("attr1").GetString().Should().Be("value1");
            attrs.GetProperty("attr2").GetInt32().Should().Be(42);

            var startNs = root.GetProperty("StartTimeUnixNano").GetUInt64();
            var endNs = root.GetProperty("EndTimeUnixNano").GetUInt64();
            startNs.Should().Be(DatetimeHelper.ToUnixNanos(start));
            endNs.Should().Be(DatetimeHelper.ToUnixNanos(end));
            endNs.Should().BeGreaterThan(startNs);

            // Duration is not part of the serialized payload
            root.TryGetProperty("Duration", out _).Should().BeFalse();
        }

        [TestMethod]
        public void Serialize_WithMissingOptionalFields_ProducesDefaults()
        {
            // Arrange
            var explicitSpanId = "explicit-span";
            var data = new InvokeAgentData(
                new Dictionary<string, object?> { { "key", "val" } },
                startTime: null,
                endTime: null,
                spanId: explicitSpanId,
                parentSpanId: null);

            // Act
            var json = LogSerializer.Serialize(data.ToDictionary());

            // Assert
            json.Should().NotBeNullOrWhiteSpace();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            root.GetProperty("Name").GetString().Should().Be("InvokeAgent");
            root.GetProperty("SpanId").GetString().Should().Be(explicitSpanId);
            root.GetProperty("StartTimeUnixNano").GetUInt64().Should().Be(0);
            root.GetProperty("EndTimeUnixNano").GetUInt64().Should().Be(0);

            // ParentSpanId should be omitted due to null (ignore when writing null)
            root.TryGetProperty("ParentSpanId", out _).Should().BeFalse();

            var attrs = root.GetProperty("Attributes");
            attrs.GetProperty("key").GetString().Should().Be("val");
        }
    }
}
