using FluentAssertions;
using Microsoft.Agents.A365.Observability.Runtime.DTOs.Builders;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Contracts;
using Microsoft.Agents.A365.Observability.Runtime.Tracing.Scopes;

namespace Microsoft.Agents.A365.Observability.Runtime.Tests.DTOs.Builders
{
    [TestClass]
    public class InvokeAgentDataBuilderTests
    {
        [TestMethod]
        public void Build_WithMinimalDetails_ReturnsInvokeAgentTelemetryWithExpectedAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            telemetry.Should().NotBeNull();
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            telemetry.Attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);
            telemetry.StartTime.Should().BeNull();
            telemetry.EndTime.Should().BeNull();
            telemetry.SpanId.Should().NotBeNullOrEmpty();
            telemetry.ParentSpanId.Should().BeNull();
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void Build_WithFullDetails_ReturnsCompleteInvokeAgentTelemetry()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails(
                "agent-123", 
                "TestAgent", 
                "Test Description",
                null,
                "auid-456",
                "agent@example.com",
                "blueprint-789",
                "tenant-999");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails, "session-456");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var request = new Request(
                "test content",
                ExecutionType.HumanToAgent,
                "session-456",
                new SourceMetadata("source-id", "source-name", Role.Human, "source-description"));
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var callerDetails = new CallerDetails("caller-123", "Caller Name", "caller@example.com");
            var conversationId = "conv-999";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId);

            // Assert
            var attributes = telemetry.Attributes;
            
            // Agent details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            attributes[OpenTelemetryConstants.GenAiAgentDescriptionKey].Should().Be("Test Description");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey);
            attributes[OpenTelemetryConstants.GenAiAgentAUIDKey].Should().Be("auid-456");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey);
            attributes[OpenTelemetryConstants.GenAiAgentUPNKey].Should().Be("agent@example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentBlueprintIdKey].Should().Be("blueprint-789");

            // Endpoint details
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);

            // Session ID
            attributes.Should().ContainKey(OpenTelemetryConstants.SessionIdKey);
            attributes[OpenTelemetryConstants.SessionIdKey].Should().Be("session-456");

            // Request details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionSourceIdKey);
            attributes[OpenTelemetryConstants.GenAiExecutionSourceIdKey].Should().Be("source-id");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiExecutionTypeKey);
            attributes[OpenTelemetryConstants.GenAiExecutionTypeKey].Should().Be("HumanToAgent");

            // Conversation ID
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            attributes[OpenTelemetryConstants.GenAiConversationIdKey].Should().Be("conv-999");

            // Caller details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            attributes[OpenTelemetryConstants.GenAiCallerIdKey].Should().Be("caller-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerNameKey);
            attributes[OpenTelemetryConstants.GenAiCallerNameKey].Should().Be("Caller Name");

            // Caller agent details
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiCallerAgentIdKey].Should().Be("caller-agent-789");
        }

        [TestMethod]
        public void Build_WithNonStandardPort_IncludesPortInAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            telemetry.Attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);
        }

        [TestMethod]
        public void Build_WithStandardPort443_ExcludesPortFromAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:443");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.ServerPortKey);
        }

        [TestMethod]
        public void Build_WithNullOptionalParameters_OmitsThoseAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.SessionIdKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiConversationIdKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiCallerIdKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiCallerAgentIdKey);
        }

        [TestMethod]
        public void Build_WithAllAgentDetailsFields_MatchesOpenTelemetryScopeAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails(
                "agent-123",
                "TestAgent",
                "Test Description",
                iconUri: null,
                agentAUID: "auid-456",
                agentUPN: "agent@example.com",
                agentBlueprintId: "blueprint-789",
                tenantId: "tenant-999");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails, "session-456");
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails);

            // Assert
            var attributes = telemetry.Attributes;
            
            // Agent details (from OpenTelemetryScope constructor)
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentNameKey);
            attributes[OpenTelemetryConstants.GenAiAgentNameKey].Should().Be("TestAgent");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentDescriptionKey);
            attributes[OpenTelemetryConstants.GenAiAgentDescriptionKey].Should().Be("Test Description");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentAUIDKey);
            attributes[OpenTelemetryConstants.GenAiAgentAUIDKey].Should().Be("auid-456");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentUPNKey);
            attributes[OpenTelemetryConstants.GenAiAgentUPNKey].Should().Be("agent@example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentBlueprintIdKey);
            attributes[OpenTelemetryConstants.GenAiAgentBlueprintIdKey].Should().Be("blueprint-789");

            // Tenant details (from OpenTelemetryScope constructor)
            attributes.Should().ContainKey(OpenTelemetryConstants.TenantIdKey);

            // InvokeAgentScope-specific attributes
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerAddressKey);
            attributes[OpenTelemetryConstants.ServerAddressKey].Should().Be("example.com");
            attributes.Should().ContainKey(OpenTelemetryConstants.ServerPortKey);
            attributes[OpenTelemetryConstants.ServerPortKey].Should().Be(8080);
            attributes.Should().ContainKey(OpenTelemetryConstants.SessionIdKey);
            attributes[OpenTelemetryConstants.SessionIdKey].Should().Be("session-456");
        }

        [TestMethod]
        public void Build_WithInputMessages_IncludesInputMessagesAttribute()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new[] { "Hello", "How are you?" };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("Hello,How are you?");
        }

        [TestMethod]
        public void Build_WithOutputMessages_IncludesOutputMessagesAttribute()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var outputMessages = new[] { "Hi there!", "I'm fine." };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                outputMessages: outputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("Hi there!,I'm fine.");
        }

        [TestMethod]
        public void Build_WithBothInputAndOutputMessages_IncludesBothAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages,
                outputMessages: outputMessages);

            // Assert
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("Hello");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("Hi");
        }

        [TestMethod]
        public void Build_WithEmptyInputMessages_OmitsInputMessagesAttribute()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var inputMessages = new string[] { };

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: inputMessages);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
        }

        [TestMethod]
        public void Build_WithNullMessages_OmitsMessagesAttributes()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                inputMessages: null,
                outputMessages: null);

            // Assert
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes.Should().NotContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
        }

        [TestMethod]
        public void Build_WithCustomTimingInformation_SetsTelemetryTimingProperties()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            var endTime = DateTimeOffset.UtcNow;

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                startTime: startTime,
                endTime: endTime);

            // Assert
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(100));
        }

        [TestMethod]
        public void Build_WithCustomSpanIds_SetsTelemetrySpanProperties()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123", "TestAgent");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var spanId = "abc123def456";
            var parentSpanId = "parent789ghi012";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                spanId: spanId,
                parentSpanId: parentSpanId);

            // Assert
            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithAllParameters_ReturnsCompleteInvokeAgentTelemetryWithAllProperties()
        {
            // Arrange
            var endpoint = new Uri("https://example.com:8080");
            var agentDetails = new AgentDetails(
                "agent-123",
                "TestAgent",
                "Test Description",
                iconUri: null,
                agentAUID: "auid-456",
                agentUPN: "agent@example.com",
                agentBlueprintId: "blueprint-789",
                tenantId: "tenant-999");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails, "session-456");
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var request = new Request(
                "test content",
                ExecutionType.HumanToAgent,
                "session-456",
                new SourceMetadata("source-id", "source-name", Role.Human, "source-description"));
            var callerAgentDetails = new AgentDetails("caller-agent-789", "CallerAgent");
            var callerDetails = new CallerDetails("caller-123", "Caller Name", "caller@example.com");
            var conversationId = "conv-999";
            var inputMessages = new[] { "Hello" };
            var outputMessages = new[] { "Hi" };
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var endTime = DateTimeOffset.UtcNow;
            var spanId = "span123";
            var parentSpanId = "parent456";

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                request,
                callerAgentDetails,
                callerDetails,
                conversationId,
                inputMessages,
                outputMessages,
                startTime,
                endTime,
                spanId,
                parentSpanId);

            // Assert
            // Attributes
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiAgentIdKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiAgentIdKey].Should().Be("agent-123");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiInputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiInputMessagesKey].Should().Be("Hello");
            telemetry.Attributes.Should().ContainKey(OpenTelemetryConstants.GenAiOutputMessagesKey);
            telemetry.Attributes[OpenTelemetryConstants.GenAiOutputMessagesKey].Should().Be("Hi");

            // Timing
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().Be(endTime);
            telemetry.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(100));

            // Span information
            telemetry.SpanId.Should().Be(spanId);
            telemetry.ParentSpanId.Should().Be(parentSpanId);
        }

        [TestMethod]
        public void Build_WithOnlyStartTime_SetsDurationToZero()
        {
            // Arrange
            var endpoint = new Uri("https://example.com");
            var agentDetails = new AgentDetails("agent-123");
            var invokeAgentDetails = new InvokeAgentDetails(endpoint, agentDetails);
            var tenantDetails = new TenantDetails(Guid.NewGuid());
            var startTime = DateTimeOffset.UtcNow;

            // Act
            var telemetry = InvokeAgentDataBuilder.Build(
                invokeAgentDetails,
                tenantDetails,
                startTime: startTime);

            // Assert
            telemetry.StartTime.Should().Be(startTime);
            telemetry.EndTime.Should().BeNull();
            telemetry.Duration.Should().Be(TimeSpan.Zero);
        }
    }
}
