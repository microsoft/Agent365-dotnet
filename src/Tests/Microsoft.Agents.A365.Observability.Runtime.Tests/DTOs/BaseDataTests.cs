// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class BaseDataTests
    {
        private sealed class TestData : BaseData
        {
            public TestData(
                IDictionary<string, object?>? attributes = null,
                DateTimeOffset? startTime = null,
                DateTimeOffset? endTime = null,
                string? spanId = null,
                string? parentSpanId = null) : base(attributes, startTime, endTime, spanId, parentSpanId) { }
            public override string Name => "Test";
        }

        [TestMethod]
        public void Constructor_WithAttributes_AssignsDictionary()
        {
            var attrs = new Dictionary<string, object?> { { "k", "v" } };
            var data = new TestData(attrs);
            data.Attributes.Should().ContainKey("k").WhoseValue.Should().Be("v");
        }

        [TestMethod]
        public void SpanId_Generated_WhenNotProvided()
        {
            var data = new TestData();
            data.SpanId.Should().NotBeNullOrEmpty();
            Guid.TryParse(data.SpanId, out _).Should().BeTrue();
        }

        [TestMethod]
        public void SpanId_UsesProvidedValue()
        {
            var id = "custom-span";
            var data = new TestData(spanId: id);
            data.SpanId.Should().Be(id);
        }

        [TestMethod]
        public void ParentSpanId_DefaultsToNull()
        {
            var data = new TestData();
            data.ParentSpanId.Should().BeNull();
        }

        [TestMethod]
        public void ParentSpanId_UsesProvidedValue()
        {
            var parent = "parent-span";
            var data = new TestData(parentSpanId: parent);
            data.ParentSpanId.Should().Be(parent);
        }

        [TestMethod]
        public void Duration_BothTimesSet_ComputesPositive()
        {
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            var end = DateTimeOffset.UtcNow;
            var data = new TestData(startTime: start, endTime: end);
            data.Duration.Should().BeCloseTo(end - start, TimeSpan.FromMilliseconds(50));
        }

        [TestMethod]
        public void Duration_OnlyStartTime_Zero()
        {
            var start = DateTimeOffset.UtcNow;
            var data = new TestData(startTime: start);
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_OnlyEndTime_Zero()
        {
            var end = DateTimeOffset.UtcNow;
            var data = new TestData(endTime: end);
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_NoTimes_Zero()
        {
            var data = new TestData();
            data.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_EndBeforeStart_Negative()
        {
            var start = DateTimeOffset.UtcNow;
            var end = start.AddMinutes(-2);
            var data = new TestData(startTime: start, endTime: end);
            data.Duration.Should().BeNegative();
            data.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(-2), TimeSpan.FromMilliseconds(50));
        }

        [TestMethod]
        public void MultipleInstances_GenerateUniqueSpanIds()
        {
            var d1 = new TestData();
            var d2 = new TestData();
            var d3 = new TestData();
            d1.SpanId.Should().NotBe(d2.SpanId);
            d1.SpanId.Should().NotBe(d3.SpanId);
            d2.SpanId.Should().NotBe(d3.SpanId);
        }

        [TestMethod]
        public void Attributes_PreserveTypes()
        {
            var attrs = new Dictionary<string, object?>
            {
                { "string", "s" },
                { "int", 1 },
                { "double", 2.5 },
                { "bool", true },
                { "null", null }
            };
            var data = new TestData(attrs);
            data.Attributes["string"].Should().BeOfType<string>();
            data.Attributes["int"].Should().BeOfType<int>();
            data.Attributes["double"].Should().BeOfType<double>();
            data.Attributes["bool"].Should().BeOfType<bool>();
            data.Attributes["null"].Should().BeNull();
        }
    }
}
