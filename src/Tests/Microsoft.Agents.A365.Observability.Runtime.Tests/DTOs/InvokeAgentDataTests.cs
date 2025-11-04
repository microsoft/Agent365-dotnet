using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs
{
    [TestClass]
    public class InvokeAgentDataTests
    {
        [TestMethod]
        public void Constructor_WithMinimalParameters_CreatesInstance()
        {
            // Arrange
            var attributes = new Dictionary<string, object?> { { "key1", "value1" } };

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.Should().NotBeNull();
            telemetry.Attributes.Should().NotBeNull();
            telemetry.Attributes.Should().ContainKey("key1");
            telemetry.Attributes["key1"].Should().Be("value1");
        }

        [TestMethod]
        public void Constructor_WithAllParameters_SetsAllProperties()
        {
            // Arrange
            var attributes = new Dictionary<string, object?> 
            { 
                { "key1", "value1" },
                { "key2", 42 }
            };
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            var endTime = DateTimeOffset.UtcNow;
            var spanId = "test-span-123";
            var parentSpanId = "parent-span-456";

            // Act
            var telemetry = new InvokeAgentData(
                attributes,
                startTime,
                endTime,
                spanId,
                parentSpanId);

            // Assert
            telemetry.Attributes.Should().ContainKey("key1");
            telemetry.Attributes["key1"].Should().Be("value1");
            telemetry.Attributes.Should().ContainKey("key2");
            telemetry.Attributes["key2"].Should().Be(42);
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void SpanId_WhenNotProvided_GeneratesGuid()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.SpanId.Should().NotBeNullOrEmpty();
            Guid.TryParse(telemetry.SpanId, out _).Should().BeTrue("SpanId should be a valid GUID");
        }

        [TestMethod]
        public void SpanId_WhenProvided_UsesProvidedValue()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var customSpanId = "custom-span-id-789";

            // Act
            var telemetry = new InvokeAgentData(attributes, spanId: customSpanId);

            // Assert
            telemetry.SpanId.Should().Be(customSpanId);
        }

        [TestMethod]
        public void Duration_WithBothStartAndEndTime_CalculatesCorrectly()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-10);
            var endTime = DateTimeOffset.UtcNow;

            // Act
            var telemetry = new InvokeAgentData(attributes, startTime, endTime);

            // Assert
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Duration_WithOnlyStartTime_ReturnsZero()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var startTime = DateTimeOffset.UtcNow;

            // Act
            var telemetry = new InvokeAgentData(attributes, startTime);

            // Assert
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_WithOnlyEndTime_ReturnsZero()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var endTime = DateTimeOffset.UtcNow;

            // Act
            var telemetry = new InvokeAgentData(attributes, endTime: endTime);

            // Assert
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_WithNoTiming_ReturnsZero()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Duration_WithEndBeforeStart_ReturnsNegativeDuration()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var startTime = DateTimeOffset.UtcNow;
            var endTime = startTime.AddMinutes(-5);

            // Act
            var telemetry = new InvokeAgentData(attributes, startTime, endTime);

            // Assert
            telemetry.Duration.Should().BeNegative();
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(-5), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void StartTime_WhenNotProvided_IsNull()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.StartTime.Should().BeNull();
        }

        [TestMethod]
        public void EndTime_WhenNotProvided_IsNull()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.EndTime.Should().BeNull();
        }

        [TestMethod]
        public void ParentSpanId_WhenNotProvided_IsNull()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.ParentSpanId.Should().BeNull();
        }

        [TestMethod]
        public void ParentSpanId_WhenProvided_UsesProvidedValue()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var parentSpanId = "parent-abc-123";

            // Act
            var telemetry = new InvokeAgentData(attributes, parentSpanId: parentSpanId);

            // Assert
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Attributes_IsReadOnly_CannotBeModified()
        {
            // Arrange
            var attributes = new Dictionary<string, object?> { { "key1", "value1" } };
            var telemetry = new InvokeAgentData(attributes);

            // Act & Assert
            telemetry.Attributes.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
        }

        [TestMethod]
        public void Attributes_AfterConstruction_ReflectsOriginalDictionary()
        {
            // Arrange
            var attributes = new Dictionary<string, object?> 
            { 
                { "key1", "value1" },
                { "key2", 123 },
                { "key3", null }
            };

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.Attributes.Should().HaveCount(3);
            telemetry.Attributes.Should().ContainKey("key1").WhoseValue.Should().Be("value1");
            telemetry.Attributes.Should().ContainKey("key2").WhoseValue.Should().Be(123);
            telemetry.Attributes.Should().ContainKey("key3").WhoseValue.Should().BeNull();
        }

        [TestMethod]
        public void Constructor_WithEmptyAttributes_CreatesEmptyDictionary()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.Attributes.Should().NotBeNull();
            telemetry.Attributes.Should().BeEmpty();
        }

        [TestMethod]
        public void Duration_WithPreciseTimeDifference_CalculatesAccurately()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();
            var startTime = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
            var endTime = new DateTimeOffset(2024, 1, 1, 10, 5, 30, TimeSpan.Zero);

            // Act
            var telemetry = new InvokeAgentData(attributes, startTime, endTime);

            // Assert
            telemetry.Duration.Should().Be(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)));
        }

        [TestMethod]
        public void MultipleInstances_GenerateUniqueSpanIds()
        {
            // Arrange
            var attributes = new Dictionary<string, object?>();

            // Act
            var telemetry1 = new InvokeAgentData(attributes);
            var telemetry2 = new InvokeAgentData(attributes);
            var telemetry3 = new InvokeAgentData(attributes);

            // Assert
            telemetry1.SpanId.Should().NotBe(telemetry2.SpanId);
            telemetry1.SpanId.Should().NotBe(telemetry3.SpanId);
            telemetry2.SpanId.Should().NotBe(telemetry3.SpanId);
        }

        [TestMethod]
        public void Constructor_WithComplexAttributeValues_PreservesAllTypes()
        {
            // Arrange
            var attributes = new Dictionary<string, object?> 
            { 
                { "string", "text" },
                { "int", 42 },
                { "long", 123456789L },
                { "double", 3.14159 },
                { "bool", true },
                { "dateTime", DateTime.UtcNow },
                { "guid", Guid.NewGuid() },
                { "null", null }
            };

            // Act
            var telemetry = new InvokeAgentData(attributes);

            // Assert
            telemetry.Attributes.Should().HaveCount(8);
            telemetry.Attributes["string"].Should().BeOfType<string>();
            telemetry.Attributes["int"].Should().BeOfType<int>();
            telemetry.Attributes["long"].Should().BeOfType<long>();
            telemetry.Attributes["double"].Should().BeOfType<double>();
            telemetry.Attributes["bool"].Should().BeOfType<bool>();
            telemetry.Attributes["dateTime"].Should().BeOfType<DateTime>();
            telemetry.Attributes["guid"].Should().BeOfType<Guid>();
            telemetry.Attributes["null"].Should().BeNull();
        }
    }
}
